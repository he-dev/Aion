using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Aion.Core.Options;
using Aion.Core.Quartz;
using Aion.Core.Quartz.JobExecutionRules;
using Aion.Core.Workflows;
using Aion.Core.Workflows.StepExecutionRules;
using Aion.Meta.Services.Mvc;
using Aion.Util;
using Aion.Util.Quartz;
using Aion.Util.Serilog;
using Aion.Util.Serilog.Enriching;
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

[assembly: InternalsVisibleTo("Aion.Tests")]


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
                var engineOptions = context.Configuration.GetRequiredSection(InstanceOptions.SectionName).Get<InstanceOptions>()!;

                // note: Main loggers are filtered in the appsettings.Serilog.json as there is no way to set up these filters here.
                // See https://github.com/serilog/serilog-expressions for all filter expressions.

                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.With<EnrichesLogEventWithActivity>()
                    .Enrich.WithProperty("Application", Program.Name)
                    .Enrich.WithProperty("Version", Program.Version)
                    .Enrich.WithProperty("Instance", engineOptions.Name)
                    .Enrich.With(new EnrichesLogEventWithDuration(ts => (int)ts.TotalMilliseconds))
                    .WriteTo.Sink(services.GetRequiredService<LogEventMapping>());
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<InstanceOptions>(context.Configuration.GetSection(InstanceOptions.SectionName));
                services.AddSingleton<IValidateOptions<InstanceOptions>, ProfileUniquenessValidation>();
                services.AddSingleton<IPostConfigureOptions<InstanceOptions>, ProfilePathRendering>();
                services.AddSingleton<LogEventMapping>();

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

                services.AddScoped<WorkflowScheduleRegistry>();
                services.AddScoped<WorkflowExecutionJob>();
                services.AddScoped<WorkflowSynchronizationJob>();

                services.AddScoped<WorkflowRendering>();
                services.AddScoped<WorkflowExecution>();
                services.AddScoped<IStepExecutionRule, StepMustBeEnabled>();
                services.AddScoped<IStepExecutionRule, StepDependsOnPrevious>();
                services.AddScoped<AsyncProcess>();
                services.AddScoped<WorkflowSynchronizationMustBeEnabled>();
                services.AddScoped<WorkflowCannotExecuteWhenDowntime>();

                services.AddQuartz(q =>
                {
                    using var serviceProvider = services.BuildServiceProvider();
                    //var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                    var instanceOptions = serviceProvider.GetRequiredService<IOptions<InstanceOptions>>().Value;
                    //var engineOptions = context.Configuration.GetRequiredSection(EngineOptions.SectionName).Get<EngineOptions>()!;

                    foreach (var profile in instanceOptions.Profiles)
                    {
                        if (!profile.Enabled)
                        {
                            //logger.LogWarning("Skipping profile '{ProfileName}' because it is not configured to sync.", profile.Name);
                            //continue;
                        }

                        var jobDetail = JobBuilder
                            .Create<WorkflowSynchronizationJob>()
                            .WithIdentity("sync-workflows", GroupName.For<WorkflowSynchronizationJob>(profile.Name))
                            .Build();

                        q.ScheduleJob<WorkflowSynchronizationJob>(trigger =>
                        {
                            trigger
                                .ForJob(jobDetail)
                                .WithIdentity("sync-workflows-cron", GroupName.For<WorkflowSynchronizationJob>(profile.Name))
                                .UsingJobData(JobDataKeys.ProfileName, profile.Name)
                                .WithCronSchedule(CronScheduleBuilder.CronSchedule(profile.Sync));
                        });

                        q.ScheduleJob<WorkflowSynchronizationJob>(trigger =>
                        {
                            trigger
                                .ForJob(jobDetail)
                                .WithIdentity("sync-workflows-once", GroupName.For<WorkflowSynchronizationJob>(profile.Name))
                                .UsingJobData(JobDataKeys.ProfileName, profile.Name)
                                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                                .StartNow();
                        });
                    }

                    q.AddTriggerListener<WorkflowSynchronizationMustBeEnabled>(GroupMatcher<TriggerKey>.GroupStartsWith(GroupName.For<WorkflowSynchronizationJob>()));
                    q.AddTriggerListener<WorkflowCannotExecuteWhenDowntime>(GroupMatcher<TriggerKey>.GroupStartsWith(GroupName.For<WorkflowExecutionJob>()));

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