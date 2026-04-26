using System;
using Aion.Core.Endpoints;
using Aion.Core.Jobs;
using Aion.Core.Services.Downtimes;
using Aion.Core.Services.Profiles;
using Aion.Core.Services.Workflows;
using Aion.Meta;
using Aion.Meta.Serilog;
using Aion.Meta.Serilog.Enriching;
using Aion.Util;
using Aion.Util.Scheduler;
using Aion.Util.Scheduler.JobExecutionRules;
using Aion.Util.Services;
using Aion.Util.Services.Synchronizations;
using Aion.Util.StepExecutionRules;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.AspNetCore;
using Quartz.Impl.Matchers;
using Serilog;
using ExecuteStep = Aion.Core.Services.Workflows.ExecuteStep;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//builder.Services.AddOptions<>();
builder.Services.ConfigureFromSection<SchedulerOptions>(builder.Configuration);
builder.Services.AddSingleton<IValidateOptions<SchedulerOptions>, SchedulerOptionsValidation>();
builder.Services.AddSingleton<IPostConfigureOptions<SchedulerOptions>, SchedulerOptionsPostConfigure>();
builder.Services.AddSingleton<MapLogEvent>();

builder
    .Configuration
    .AddJsonFile("appsettings.Serilog.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Profiles.json", optional: false, reloadOnChange: true)
    .AddCommandLine(args);

builder
    .Logging
    .ClearProviders();

builder
    .Host
    .UseSerilog((context, services, configuration) =>
    {
        var schedulerOptions = services.GetRequiredService<IOptions<SchedulerOptions>>().Value;

        // note: Main loggers are filtered in the appsettings.Serilog.json as there is no way to set up these filters here.
        // See https://github.com/serilog/serilog-expressions for all filter expressions.

        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.With<EnrichesLogEventWithActivity>()
            .Enrich.FromLogContext()
            //.Enrich.WithProperty("Application", Program.Name)
            //.Enrich.WithProperty("Version", Program.Version)
            .Enrich.WithProperty("Instance", schedulerOptions.Name)
            .Enrich.With(new EnrichesLogEventWithDuration(ts => (int)ts.TotalMilliseconds, "DurationMs"))
            .WriteTo.Sink(services.GetRequiredService<MapLogEvent>());
    });

builder.Services.AddSingleton(x => x.GetRequiredService<IHostEnvironment>().ContentRootFileProvider);

builder.Services.AddScoped<WorkflowJob>();
builder.Services.AddScoped<ProfileJob>();

// meta: The order these services are registered specifies their execution order.
builder.Services.AddTransient<SynchronizeWorkflowAction, ScheduleCustomWorkflow>();
builder.Services.AddTransient<SynchronizeWorkflowAction, UnscheduleDisabledWorkflow>();
builder.Services.AddTransient<SynchronizeWorkflowAction, UnscheduleEmptyWorkflow>();
builder.Services.AddTransient<SynchronizeWorkflowAction, RescheduleChangedWorkflow>();
builder.Services.AddTransient<SynchronizeWorkflowAction, ScheduleRegularWorkflow>();

// meta: These two services must be singletons because otherwise cannot inject them into ITriggerListener instances.
builder.Services.AddSingleton<FindWorkflows>();
builder.Services.AddSingleton<GetProfile>();

builder.Services.AddScoped<CreateWorkflow>();
builder.Services.AddScoped<ExecuteWorkflow>();
builder.Services.AddScoped<ExecuteStep>();
builder.Services.AddScoped<IStepExecutionRule, StepMustBeEnabled>();
builder.Services.AddScoped<IStepExecutionRule, StepDependsOnPrevious>();
builder.Services.AddScoped<StartProcess>();
builder.Services.AddScoped<WorkflowSynchronizationMustBeEnabled>();
builder.Services.AddScoped<WorkflowCannotExecuteDuringDowntime>();

builder.Services.AddQuartz(configure =>
{
    configure.AddTriggerListener<WorkflowSynchronizationMustBeEnabled>(GroupMatcher<TriggerKey>.GroupStartsWith(nameof(ProfileJob)));
    configure.AddTriggerListener<WorkflowCannotExecuteDuringDowntime>(GroupMatcher<TriggerKey>.GroupStartsWith(nameof(WorkflowJob)));

    // note: The docs say that the default is 1 minute.
    configure.MisfireThreshold = TimeSpan.FromMinutes(2);
});


builder.Services.AddQuartzServer(options =>
{
    var schedulerOptions = builder.Configuration.GetRequiredSection("Scheduler").Get<SchedulerOptions>()!;

    options.AwaitApplicationStarted = true;
    options.WaitForJobsToComplete = true;
    options.StartDelay = schedulerOptions.StartDelay;
});


builder.Services.AddScoped<GetWorkflowsInfo>();
builder.Services.AddScoped<SynchronizeWorkflow>();
builder.Services.AddScoped<SynchronizeProfile>();
builder.Services.AddScoped<ScheduleProfile>();
builder.Services.AddScoped<ExecuteWorkflow>();
builder.Services.AddScoped<GetProfileTriggers>();
builder.Services.AddScoped<GetDowntimes>();
builder.Services.AddScoped<StartDowntime>();
builder.Services.AddScoped<EndDowntime>();

builder.Services.AddProblemDetails();
//builder.Services.AddExceptionHandler<WorkflowNotExecutableExceptionHandler>();

var app = builder.Build();

// todo: new logger test
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    using var step = logger.Output.BeginScope<Contracts.Workflow.ExecuteStep.Now>(("StepIndex", 7));
    step.LogNote("This step has a note.");
    step.LogStatus(new Contracts.Workflow.ExecuteStep.Now.Ok(7));
    step.LogStatus(new Contracts.DeleteFile.Force.Ok("test.txt"));

    logger.LogText("This is a log text.");
    logger.Engine.LogStatus(new Contracts.DeleteFile.Force.Ok("test.txt"));
}

//app.UseExceptionHandler();
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapWorkflows();
app.MapSchedules();
app.MapDowntimes();

using (var scope = app.Services.CreateScope())
{
    var getProfile = scope.ServiceProvider.GetRequiredService<GetProfile>();
    var scheduleProfile = scope.ServiceProvider.GetRequiredService<ScheduleProfile>();
    foreach (var profile in getProfile.All())
    {
        await scheduleProfile.For(profile);
    }
}

app.Run();