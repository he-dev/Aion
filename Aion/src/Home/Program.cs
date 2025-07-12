using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Home.Jobs;
using Aion.Util.Serilog;
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
            .ConfigureLogging(builder => { builder.ClearProviders(); })
            .UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.With(new TimeSpanEnricher(ts => Math.Round(ts.TotalSeconds, 1)))
                    .WriteTo.Sink(services.GetRequiredService<WorkflowSink>());
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<WorkflowDirectoryOptions>(context.Configuration.GetSection("WorkflowDirectory"));
                services.Configure<SynchronizationJobOptions>(context.Configuration.GetSection("SynchronizationJob"));

                services.AddSingleton<IPostConfigureOptions<WorkflowDirectoryOptions>, WorkflowDirectoryPostConfigure>();
                services.AddSingleton<WorkflowSink>();

                services
                    .AddControllers()
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

                services.AddSingleton<WorkflowScheduler>();
                services.AddSingleton<WorkflowScheduler.Collection>();
                services.AddSingleton<WorkflowProcess>();
                services.AddSingleton<WorkflowDirectory>();
                services.AddSingleton<WorkflowMaintenance>();

                services.AddScoped<RegularWorkflowJob>();
                services.AddScoped<OnDemandWorkflowJob>();
                services.AddScoped<SynchronizationJob>();

                services.AddSingleton<SynchronizationJobTriggerListener>();
                services.AddSingleton<RegularWorkflowJobTriggerListener>();

                services.AddQuartz(q =>
                {
                    var synchronizationJobOptions = context.Configuration.GetRequiredSection("SynchronizationJob").Get<SynchronizationJobOptions>()!;
                    var jobDetail = SynchronizationJob.CreateJobDetail();

                    q.ScheduleJob<SynchronizationJob>(trigger =>
                    {
                        trigger
                            .ForJob(jobDetail)
                            .WithIdentity("sync-jobs-by-cron", JobGroupNames.Services)
                            .WithCronSchedule(CronScheduleBuilder.CronSchedule(synchronizationJobOptions.Cron));
                    });

                    q.ScheduleJob<SynchronizationJob>(trigger =>
                    {
                        trigger
                            .ForJob(jobDetail)
                            .WithIdentity("sync-jobs-by-start-now", JobGroupNames.Services)
                            .StartNow()
                            .WithSimpleSchedule(x => x.WithRepeatCount(0));
                    });

                    q.AddTriggerListener<SynchronizationJobTriggerListener>(GroupMatcher<TriggerKey>.GroupEquals(JobGroupNames.Services));
                    q.AddTriggerListener<RegularWorkflowJobTriggerListener>(GroupMatcher<TriggerKey>.GroupEquals(JobGroupNames.Workflows));

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

internal static class JobGroupNames
{
    public const string Workflows = nameof(Workflows);
    public const string Services = nameof(Services);
}

public record QuartzServerOptions
{
    public int StartDelaySeconds { get; init; }
}

public interface ITimeZoned
{
    TimeZoneInfo TimeZone { get; }
}