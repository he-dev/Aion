using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Features;
using Aion.Core.Features.WhenTriggersFire;
using Aion.Core.Modules;
using Aion.Home.Jobs;
using Aion.Util;
using Aion.Util.Quartz;
using Aion.Util.Serilog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.AspNetCore;
using Quartz.Impl.Matchers;
using Serilog;
using Serilog.Events;

namespace Aion.Home;

public class Program
{
    public const string Name = "Aion";

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
                    ImmutableHashSet<ProcessMessageSource>.Empty
                        .Add(ProcessMessageSource.StdOut)
                        .Add(ProcessMessageSource.StdErr);

                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.With<EnrichesLogEventWithActivityIds>()
                    .Enrich.WithProperty("AppName", Program.Name)
                    .Enrich.WithProperty("ProfileName", engineOptions.Name)
                    .Enrich.With(new TimeSpanEnricher(ts => Math.Round(ts.TotalSeconds, 1)))
                    .WriteTo.Logger(logger =>
                    {
                        logger
                            // core: The workflow-sink may only log events that contain the workflow-name property.
                            .Filter.ByIncludingOnly(e => e.Properties.ContainsKey(nameof(WorkflowLogEventSignature.WorkflowName)))
                            // core: Don't log raw console output.
                            .Filter.ByExcluding(e =>
                            {
                                return
                                    e.Properties.TryGetValue(nameof(ProcessMessageSource), out var value)
                                    && value is ScalarValue { Value: string scalar }
                                    && consoleStreamTypes.Contains(Enum.Parse<ProcessMessageSource>(scalar));
                            })
                            .WriteTo.Sink(services.GetRequiredService<MapsLogEvents>());
                    })
                    .WriteTo.Logger(logger =>
                    {
                        logger
                            // core: The console-sink may only log events that contain the stream type.
                            .Filter.ByIncludingOnly(e => e.Properties.ContainsKey(nameof(ProcessMessageSource)))
                            .WriteTo.Sink(services.GetRequiredService<MapsLogEvents>());
                    })
                    ;
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<EngineOptions>(context.Configuration.GetSection(EngineOptions.SectionName));
                services.AddSingleton<IPostConfigureOptions<EngineOptions>, EngineOptions.RenderPaths>();
                services.AddSingleton<MapsLogEvents>();
                services.AddSingleton<MapsLogEvents>();

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

                services.AddSingleton<SchedulesWorkflowExecution>();
                services.AddSingleton<FindsTriggers>();
                services.AddSingleton<ExecutesWorkflow>();
                services.AddSingleton<FindsWorkflows>();
                services.AddSingleton<LocksWorkflows>();

                services.AddSingleton<FindsLoggingPreset>();

                services.AddScoped<ExecutesWorkflowOnSchedule>();
                services.AddScoped<ExecutesWorkflowOnDemand>();
                services.AddScoped<SynchronizesProfile>();

                services.AddSingleton<CanVetoProfileSynchronization>();
                services.AddSingleton<CanVetoWorkflowExecution>();

                services.AddQuartz(q =>
                {
                    var engineOptions = context.Configuration.GetRequiredSection(EngineOptions.SectionName).Get<EngineOptions>()!;

                    foreach (var profile in engineOptions.Profiles)
                    {
                        var jobDetail = JobBuilder
                            .Create<SynchronizesProfile>()
                            .WithIdentity("sync-profile", new GroupName<SynchronizesProfile>(profile.Name))
                            .UsingJobData(JobDataKeys.ProfileName, profile.Name)
                            .UsingJobData(JobDataKeys.ProfilePath, profile.Path)
                            .Build();


                        q.ScheduleJob<SynchronizesProfile>(trigger =>
                        {
                            trigger
                                .ForJob(jobDetail)
                                .WithIdentity("run-by-cron", new GroupName<SynchronizesProfile>(profile.Name))
                                .UsingJobData(JobDataKeys.ProfileName, profile.Name)
                                .UsingJobData(JobDataKeys.ProfilePath, profile.Path)
                                .WithCronSchedule(CronScheduleBuilder.CronSchedule(profile.Sync));
                        });

                        q.ScheduleJob<SynchronizesProfile>(trigger =>
                        {
                            trigger
                                .ForJob(jobDetail)
                                .WithIdentity("run-once", new GroupName<SynchronizesProfile>(profile.Name))
                                .UsingJobData(JobDataKeys.ProfileName, profile.Name)
                                .UsingJobData(JobDataKeys.ProfilePath, profile.Path)
                                .StartNow()
                                .WithSimpleSchedule(x => x.WithRepeatCount(0));
                        });
                    }

                    q.AddTriggerListener<CanVetoProfileSynchronization>(GroupMatcher<TriggerKey>.GroupStartsWith(new GroupName<SynchronizesProfile>()));
                    q.AddTriggerListener<CanVetoWorkflowExecution>(GroupMatcher<TriggerKey>.GroupStartsWith(new GroupName<ExecutesWorkflowOnSchedule>()));

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

public class CreatesAbsoluteRouteWhenStartsWithColon : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            // meta: Find all route templates defined on the controller
            var controllerRouteTemplates =
                controller
                    .Selectors
                    .Select(s => s.AttributeRouteModel?.Template)
                    .Where(t => t is not null)
                    .ToList();

            if (!controllerRouteTemplates.Any())
            {
                // note: This actually should be an error...
            }

            foreach (var action in controller.Actions)
            {
                foreach (var selector in action.Selectors)
                {
                    if (selector.AttributeRouteModel is { Template: { } actionRouteTemplate } && actionRouteTemplate.StartsWith(":"))
                    {
                        // core: Create an absolute route to override automatic joining.
                        var combinedTemplate = "/" + controllerRouteTemplates.First() + actionRouteTemplate;

                        // core: Overwrite the action's original route.
                        selector.AttributeRouteModel = new AttributeRouteModel
                        {
                            Template = combinedTemplate,
                            Name = selector.AttributeRouteModel.Name,
                            Order = selector.AttributeRouteModel.Order,
                        };
                    }
                }
            }
        }
    }
}