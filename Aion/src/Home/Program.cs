using System;
using Aion.Core.Commands;
using Aion.Core.Commands.Workflows;
using Aion.Core.Options;
using Aion.Core.Quartz;
using Aion.Core.Quartz.JobExecutionRules;
using Aion.Core.Quartz.Jobs;
using Aion.Core.Workflows;
using Aion.Core.Workflows.StepExecutionRules;
using Aion.Home.Endpoints;
using Aion.Meta;
using Aion.Util;
using Aion.Util.Quartz;
using Aion.Util.Serilog;
using Aion.Util.Serilog.Enriching;
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
builder.Services.AddSingleton<IValidateOptions<SchedulerOptions>, ProfilePathValidation>();
builder.Services.AddSingleton<IPostConfigureOptions<SchedulerOptions>, SchedulerOptionsPostConfigure>();
builder.Services.AddSingleton<LogEventMapping>();

builder
    .Host
    .ConfigureAppConfiguration((host, config) =>
    {
        config
            .AddJsonFile("appsettings.Serilog.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Profiles.json", optional: false, reloadOnChange: true)
            .AddCommandLine(args);
    })
    .ConfigureLogging(b => { b.ClearProviders(); })
    .UseSerilog((context, services, configuration) =>
    {
        var engineOptions = services.GetRequiredService<IOptions<SchedulerOptions>>().Value;

        // note: Main loggers are filtered in the appsettings.Serilog.json as there is no way to set up these filters here.
        // See https://github.com/serilog/serilog-expressions for all filter expressions.

        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.With<EnrichesLogEventWithActivity>()
            //.Enrich.WithProperty("Application", Program.Name)
            //.Enrich.WithProperty("Version", Program.Version)
            .Enrich.WithProperty("Instance", engineOptions.Name)
            .Enrich.With(new EnrichesLogEventWithDuration(ts => (int)ts.TotalMilliseconds))
            .WriteTo.Sink(services.GetRequiredService<LogEventMapping>());
    });

builder.Services.AddSingleton(x => x.GetRequiredService<IHostEnvironment>().ContentRootFileProvider);

builder.Services.AddScoped<WorkflowScheduleRegistry>();
builder.Services.AddScoped<WorkflowJob>();
builder.Services.AddScoped<SynchronizationJob>();

builder.Services.AddScoped<RenderWorkflow>();
builder.Services.AddScoped<ExecuteWorkflow>();
builder.Services.AddScoped<IStepExecutionRule, StepMustBeEnabled>();
builder.Services.AddScoped<IStepExecutionRule, StepDependsOnPrevious>();
builder.Services.AddScoped<AsyncProcess>();
builder.Services.AddScoped<WorkflowSynchronizationMustBeEnabled>();
builder.Services.AddScoped<WorkflowCannotExecuteWhenDowntime>();

builder.Services.AddQuartz(configure =>
{
    configure.AddTriggerListener<WorkflowSynchronizationMustBeEnabled>(GroupMatcher<TriggerKey>.GroupStartsWith(GroupName.For<SynchronizationJob>()));
    configure.AddTriggerListener<WorkflowCannotExecuteWhenDowntime>(GroupMatcher<TriggerKey>.GroupStartsWith(GroupName.For<WorkflowJob>()));

    // note: The docs say that the default is 1 minute.
    configure.MisfireThreshold = TimeSpan.FromMinutes(2);
});


builder.Services.AddQuartzServer(options =>
{
    var quartzServerOptions = builder.Configuration.GetRequiredSection("QuartzServer").Get<QuartzServerOptions>()!;

    options.AwaitApplicationStarted = true;
    options.WaitForJobsToComplete = true;
    options.StartDelay = TimeSpan.FromSeconds(quartzServerOptions.StartDelaySeconds);
});

builder.Services.AddWorkflowCommands();
builder.Services.AddScheduleCommands();
builder.Services.AddDowntimeCommands();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<WorkflowNotExecutableExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
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
    var scheduleSynchronization = ActivatorUtilities.CreateInstance<ScheduleSynchronization>(scope.ServiceProvider);
    await scheduleSynchronization.Execute();
}

app.Run();