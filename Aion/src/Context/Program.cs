using System;
using Aion.Context.Endpoints;
using Aion.Context.Jobs;
using Aion.Context.Services;
using Aion.Context.Services.Commands;
using Aion.Modules.Scheduler;
using Aion.Modules.Scheduler.JobExecutionRules;
using Aion.Modules.Services;
using Aion.Modules.Services.Queries;
using Aion.Modules.StepExecutionRules;
using Aion.Toolbox;
using Aion.Toolbox.Serilog;
using Aion.Toolbox.Serilog.Enriching;
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
            //.Enrich.WithProperty("Application", Program.Name)
            //.Enrich.WithProperty("Version", Program.Version)
            .Enrich.WithProperty("Instance", schedulerOptions.Name)
            .Enrich.With(new EnrichesLogEventWithDuration(ts => (int)ts.TotalMilliseconds))
            .WriteTo.Sink(services.GetRequiredService<MapLogEvent>());
    });

builder.Services.AddSingleton(x => x.GetRequiredService<IHostEnvironment>().ContentRootFileProvider);

builder.Services.AddScoped<WorkflowJob>();
builder.Services.AddScoped<ProfileJob>();

// meta: The order these services are registered specifies their execution order.
builder.Services.AddTransient<ISynchronizeWorkflow, ScheduleCustomWorkflow>();
builder.Services.AddTransient<ISynchronizeWorkflow, UnscheduleDisabledWorkflow>();
builder.Services.AddTransient<ISynchronizeWorkflow, UnscheduleEmptyWorkflow>();
builder.Services.AddTransient<ISynchronizeWorkflow, RescheduleChangedWorkflow>();
builder.Services.AddTransient<ISynchronizeWorkflow, ScheduleNewWorkflow>();
builder.Services.AddTransient<ISynchronizeWorkflow, IgnoreWorkflow>();

builder.Services.AddScoped<GetProfile>();
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

builder.Services.AddWorkflowCommands();
builder.Services.AddScheduleCommands();
builder.Services.AddDowntimeCommands();

builder.Services.AddProblemDetails();
//builder.Services.AddExceptionHandler<WorkflowNotExecutableExceptionHandler>();

var app = builder.Build();

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
    await ActivatorUtilities
        .CreateInstance<ScheduleProfile>(scope.ServiceProvider)
        .Invoke();
}

app.Run();