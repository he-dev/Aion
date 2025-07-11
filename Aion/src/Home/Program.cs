using System;
using System.CommandLine;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Home.Jobs;
using Aion.Util.Serilog;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.AspNetCore;
using Serilog;

namespace Aion.Home;

public class Program
{
    public static async Task Main(params string[] args)
    {
        var stopwatch = Stopwatch.StartNew();

        var disableSyncOption = new Option<bool>("--app-disable-sync", "Disable the sync job.") { IsRequired = false };
        var rootCommand = new RootCommand { disableSyncOption };
        var commandLine = rootCommand.Parse(args);

        // if (commandLine.GetValueForOption(debugOption) is false && commandLine.Tokens.Any(t => t.Value == "--url") == false)
        // {
        //     var preConfig = new ConfigurationBuilder()
        //         .SetBasePath(AppContext.BaseDirectory)
        //         .AddJsonFile("appsettings.json", optional: false)
        //         .Build();
        //
        //     args = new[] { "--url", preConfig["AionApi:Url"] };
        // }
        // else
        // {
        //     args = commandLine.UnparsedTokens.ToArray();
        // }

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<WorkflowDirectoryOptions>(builder.Configuration.GetSection("WorkflowDirectory"));
        builder.Services.Configure<SynchronizationJobOptions>(builder.Configuration.GetSection("SynchronizationJob"));

        builder.Services.AddSingleton<IPostConfigureOptions<WorkflowDirectoryOptions>, WorkflowDirectoryPostConfigure>();
        builder.Services.AddSingleton<WorkflowSink>();

        builder.Logging.ClearProviders();
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.With(new TimeSpanEnricher(ts => Math.Round(ts.TotalSeconds, 1)))
                .WriteTo.Sink(services.GetRequiredService<WorkflowSink>());

            // note: This was a nice proof-of-concept experiment, but it does not work here. WorkflowSink replaces it.
            // .WriteTo.Map(keyPropertyName: "WorkflowName", defaultKey: null, (workflowName, writeTo) =>
            // {
            //     // note: defaultKey can be null, according to the docs, but it has invalid annotations that make Rider unhappy.
            //     if (workflowName is not null)
            //     {
            //         var fileName = System.IO.Path.Combine(workflowLogOptions.DirectoryPath, $"{workflowName}.log");
            //         writeTo.File(
            //             fileName,
            //             outputTemplate: workflowLogOptions.OutputTemplate,
            //             rollingInterval: workflowLogOptions.RollingInterval,
            //             retainedFileCountLimit: workflowLogOptions.RetainedFileCountLimit
            //         );
            //     }
            // });
        });

        var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();

        builder.Services.AddControllers();
        //.AddJsonOptions(options => { options.JsonSerializerOptions.Converters.Add(new WorkflowIssueConverter()); });

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // .. There's no way these settings are missing so suppress the null warnings.


        builder.Services.AddSingleton(services => services.GetRequiredService<IHostEnvironment>().ContentRootFileProvider);
        builder.Services.AddSingleton<WorkflowScheduler>();
        builder.Services.AddSingleton<WorkflowScheduler.Collection>();
        builder.Services.AddSingleton<WorkflowProcess>();
        builder.Services.AddSingleton<WorkflowDirectory>();
        builder.Services.AddSingleton<WorkflowMaintenance>();

        builder.Services.AddScoped<RegularWorkflowJob>();
        builder.Services.AddScoped<OnDemandWorkflowJob>();
        builder.Services.AddScoped<SynchronizationJob>();

        builder.Services.AddQuartz(q =>
        {
            if (commandLine.GetValueForOption(disableSyncOption) is false)
            {
                // ?? Use the same job but with two triggers so they don't run at the same time.
                var jobDetail = SynchronizationJob.CreateJobDetail();

                try
                {
                    var synchronizationJobOptions = builder.Configuration.GetRequiredSection("SynchronizationJob").Get<SynchronizationJobOptions>()!;

                    q.ScheduleJob<SynchronizationJob>(trigger =>
                    {
                        trigger
                            .ForJob(jobDetail)
                            .WithCronSchedule(CronScheduleBuilder.CronSchedule(synchronizationJobOptions.Cron));
                    });

                    q.ScheduleJob<SynchronizationJob>(trigger =>
                    {
                        trigger
                            .ForJob(jobDetail)
                            .StartNow();
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error scheduling the synchronization job.");
                    throw;
                }
            }
            else
            {
                logger.LogWarning("Synchronization job is disabled.");
            }
        });

        builder.Services.AddQuartzServer(options =>
        {
            var quartzServerOptions = builder.Configuration.GetRequiredSection("QuartzServer").Get<QuartzServerOptions>()!;

            options.AwaitApplicationStarted = true;
            options.WaitForJobsToComplete = true;
            options.StartDelay = TimeSpan.FromSeconds(quartzServerOptions.StartDelaySeconds);
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        logger.LogInformation("Everything initialized in {Elapsed} seconds. Starting up...", stopwatch.Elapsed);

        await app.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return null;
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