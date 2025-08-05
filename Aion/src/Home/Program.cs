using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services;
using Aion.Core.Services.Scheduling;
using Aion.Core.Services.WhenTriggersFire;
using Aion.Core.StepExecutionRules;
using Aion.Home.Jobs;
using Aion.Meta.Mvc;
using Aion.Util.Quartz;
using Aion.Util.Serilog;
using Aion.Util.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.AspNetCore;
using Quartz.Impl.Matchers;
using Serilog;

namespace Aion.Home;

public class Program
{
    public const string Name = "Aion";
    public const string Version = "3.0.0";

    public static async Task Main(params string[] args)
    {
        var stopwatch = Stopwatch.StartNew();
        var builder = CreateHostBuilder(args);
        var app = builder.Build();

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Everything initialized in {Elapsed} seconds. Starting up...", stopwatch.Elapsed);

        await app.RunAsync();
    }

    // note: This must be public for the WebApplicationFactory.
    // ReSharper disable once MemberCanBePrivate.Global
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host
            .CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((host, config) => { config.AddJsonFile("appsettings.Serilog.json", optional: true, reloadOnChange: true); })
            .ConfigureLogging(builder => { builder.ClearProviders(); })
            .UseSerilog((context, services, configuration) =>
            {
                var engineOptions = context.Configuration.GetRequiredSection(EngineOptions.SectionName).Get<EngineOptions>()!;

                // note: Main loggers are filtered in the appsettings.Serilog.json as there is no way to set up these filters here.
                // See https://github.com/serilog/serilog-expressions for all filter expressions.

                var consoleStreamTypes =
                    ImmutableHashSet<ConsoleStreamType>.Empty
                        .Add(ConsoleStreamType.StdOut)
                        .Add(ConsoleStreamType.StdErr);

                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.With<EnrichesLogEventWithActivity>()
                    .Enrich.WithProperty("Application", Program.Name)
                    .Enrich.WithProperty("Version", Program.Version)
                    .Enrich.WithProperty("Instance", engineOptions.Instance)
                    .Enrich.With(new EnrichesLogEventWithDuration(ts => (int)ts.TotalMilliseconds))
                    .WriteTo.Sink(services.GetRequiredService<MapsLogEvent>())
                    // .WriteTo.Logger(logger =>
                    // {
                    //     logger
                    //         // core: The workflow-sink may only log events that contain the workflow-name property.
                    //         .Filter.ByIncludingOnly(e => e.Properties.ContainsKey(nameof(WorkflowLogEventSignature.WorkflowName)))
                    //         // core: Don't log raw console output.
                    //         .Filter.ByExcluding(e =>
                    //         {
                    //             return
                    //                 e.Properties.TryGetValue(nameof(ProcessMessageSource), out var value)
                    //                 && value is ScalarValue { Value: string scalar }
                    //                 && consoleStreamTypes.Contains(Enum.Parse<ProcessMessageSource>(scalar));
                    //         })
                    //         .WriteTo.Sink(services.GetRequiredService<MapsLogEvents>());
                    // })
                    // .WriteTo.Logger(logger =>
                    // {
                    //     logger
                    //         // core: The console-sink may only log events that contain the stream type.
                    //         //.Filter.ByIncludingOnly(e => e.Properties.ContainsKey(nameof(ProcessMessageSource)))
                    //         .WriteTo.Sink(services.GetRequiredService<MapsLogEvents>());
                    // })
                    ;
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<EngineOptions>(context.Configuration.GetSection(EngineOptions.SectionName));
                services.AddSingleton<IValidateOptions<EngineOptions>, EngineOptions.EnsuresPathsUniqueness>();
                services.AddSingleton<IPostConfigureOptions<EngineOptions>, EngineOptions.RenderPaths>();
                services.AddSingleton<MapsLogEvent>();
                services.AddSingleton<MapsLogEvent>();

                services
                    .AddControllers(options => { options.Conventions.Add(new CreatesAbsoluteRouteWhenStartsWithColon()); })
                    // meta: This is a must for endpoints.MapControllers to work.
                    .AddApplicationPart(typeof(Program).Assembly);
                //.AddJsonOptions(options => { options.JsonSerializerOptions.Converters.Add(new WorkflowIssueConverter()); });

                // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
                services.AddEndpointsApiExplorer();
                services.AddSwaggerGen(options =>
                {
                    // hack: Custom schema is necessary because Swagger otherwise will crash on types with same names.
                    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);

                    // note: Or something like this:
                    // if (type.DeclaringType != null)
                    // {
                    //     return $"{type.DeclaringType.Name}.{type.Name}";
                    // }
                    // return type.Name;
                });

                services.AddSingleton(x => x.GetRequiredService<IHostEnvironment>().ContentRootFileProvider);

                services.AddScoped<SchedulesWorkflowCron>();
                services.AddScoped<SchedulesWorkflowOnce>();
                services.AddScoped<CancelsWorkflowSchedule>();

                services.AddScoped<ExecutesWorkflowCron>();
                services.AddScoped<ExecutesWorkflowOnce>();
                services.AddScoped<SynchronizesWorkflows>();

                services.AddScoped<ExecutesWorkflow>();
                services.AddScoped<IStepExecutionRule, StepMustBeEnabled>();
                services.AddScoped<IStepExecutionRule, StepDependsOnPrevious>();
                services.AddScoped<StartsProcessAsync>();


                services.AddScoped<FindsTriggers>();

                services.AddScoped<CanVetoProfileSynchronization>();
                services.AddScoped<CanVetoWorkflowExecution>();

                services.AddQuartz(q =>
                {
                    var engineOptions = context.Configuration.GetRequiredSection(EngineOptions.SectionName).Get<EngineOptions>()!;

                    foreach (var profile in engineOptions.Profiles)
                    {
                        if (string.IsNullOrEmpty(profile.Sync))
                        {
                            continue;
                        }

                        var jobDetail = JobBuilder
                            .Create<SynchronizesWorkflows>()
                            .WithIdentity("sync-workflows", JobGroupName.From<SynchronizesWorkflows>(profile.Name))
                            .Build();

                        q.ScheduleJob<SynchronizesWorkflows>(trigger =>
                        {
                            trigger
                                .ForJob(jobDetail)
                                .WithIdentity("sync-workflows-cron", JobGroupName.From<SynchronizesWorkflows>(profile.Name))
                                .UsingJobData(JobDataKeys.ProfileName, profile.Name)
                                .WithCronSchedule(CronScheduleBuilder.CronSchedule(profile.Sync));
                        });

                        q.ScheduleJob<SynchronizesWorkflows>(trigger =>
                        {
                            trigger
                                .ForJob(jobDetail)
                                .WithIdentity("sync-workflows-now", JobGroupName.From<SynchronizesWorkflows>(profile.Name))
                                .UsingJobData(JobDataKeys.ProfileName, profile.Name)
                                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                                .StartNow();
                        });
                    }

                    q.AddTriggerListener<CanVetoProfileSynchronization>(GroupMatcher<TriggerKey>.GroupStartsWith(JobGroupName.From<SynchronizesWorkflows>()));
                    q.AddTriggerListener<CanVetoWorkflowExecution>(GroupMatcher<TriggerKey>.GroupStartsWith(JobGroupName.From<ExecutesWorkflowCron>()));

                    // note: The docs say that the default is 1 minute.
                    q.MisfireThreshold = TimeSpan.FromMinutes(2);
                });

                services.AddQuartzServer(options =>
                {
                    var quartzServerOptions = context.Configuration.GetRequiredSection("QuartzServer").Get<QuartzServerOptions>()!;

                    options.AwaitApplicationStarted = true;
                    options.WaitForJobsToComplete = true;
                    options.StartDelay = TimeSpan.FromSeconds(quartzServerOptions.StartDelaySeconds);
                });
            })
            .ConfigureWebHostDefaults(builder =>
            {
                builder.Configure((context, app) =>
                {
                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        app.UseSwagger();
                        app.UseSwaggerUI();
                    }

                    app.UseHttpsRedirection();
                    // meta: This is a must for UseEndpoints() to work.
                    app.UseRouting();
                    app.UseAuthorization();
                    // meta: This is a must for controllers to work.
                    app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
                });
            });
    }
}

public record QuartzServerOptions
{
    public int StartDelaySeconds { get; init; }
}