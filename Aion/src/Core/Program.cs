using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Jobs;
using Aion.Core.Utilities;
using Aion.Core.Workflows;
using Aion.Util;
using Aion.Util.Yaml;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Web;
using Quartz;
using Quartz.AspNetCore;

namespace Aion.Core;

public class Program
{
    public static async Task Main(params string[] args)
    {
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


        NLog.LogManager.Setup().LoadConfigurationFromFile("NLog.config");

        var builder = WebApplication.CreateBuilder(args);

        builder.Logging.ClearProviders();
        builder.Host.UseNLog();

        var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();

        builder.Services.AddControllers(options =>
        {
            options.InputFormatters.Insert(0, new YamlInputFormatter());
            options.OutputFormatters.Insert(0, new YamlOutputFormatter());
        });

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // todo: setup later
        // builder.Services.AddSingleton<LogAction>(_ => LogActionBuilder.CreateDefault().Use<LogToConsole>().Use<LogToNLog>().Build());
        // builder.Services.AddSingleton(typeof(LoggerFactory));
        // builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        // builder.Services.AddWiretap();

        // .. There's no way these settings are missing so suppress the null warnings.
        var workflowEngineOptions = builder.Configuration.GetRequiredSection("WorkflowEngine").Get<WorkflowEngineOptions>()!;
        var synchronizationJobOptions = builder.Configuration.GetRequiredSection("SynchronizationJob").Get<SynchronizationJobOptions>()!;
        var quartzServerOptions = builder.Configuration.GetRequiredSection("QuartzServer").Get<QuartzServerOptions>()!;
        var maintenanceTokenOptions = builder.Configuration.GetRequiredSection("QuartzServer").Get<MaintenanceTokenOptions>()!;

        builder.Services.Configure<WorkflowEngineOptions>(builder.Configuration.GetSection("WorkflowEngine"));
        builder.Services.Configure<SynchronizationJobOptions>(builder.Configuration.GetSection("SynchronizationJob"));
        builder.Services.Configure<MaintenanceTokenOptions>(builder.Configuration.GetSection("MaintenanceToken"));

        builder.Services.AddSingleton(services => services.GetRequiredService<IHostEnvironment>().ContentRootFileProvider);
        builder.Services.AddSingleton<WorkflowSchedule>();
        builder.Services.AddSingleton<WorkflowSchedule.Collection>();
        builder.Services.AddSingleton<WorkflowProcess>();
        builder.Services.AddSingleton<IAsyncProcess, AsyncProcess>();
        builder.Services.AddSingleton<WorkflowDirectory>();
        builder.Services.AddSingleton<MaintenanceToken>();

        builder.Services.AddScoped<RegularWorkflowJob>();
        builder.Services.AddScoped<AdHocWorkflowJob>();
        builder.Services.AddScoped<SynchronizationJob>();

        builder.Services.AddQuartz(q =>
        {
            if (commandLine.GetValueForOption(disableSyncOption) is false)
            {
                try
                {
                    q.ScheduleJob<SynchronizationJob>(trigger =>
                    {
                        trigger
                            .WithIdentity("workflow-synchronization-cron", JobGroupNames.Services)
                            .WithCronSchedule(CronScheduleBuilder.CronSchedule(synchronizationJobOptions.Cron));
                    });

                    q.ScheduleJob<SynchronizationJob>(trigger =>
                    {
                        trigger
                            .WithIdentity("workflow-synchronization-start-now", JobGroupNames.Services)
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
        // app.UseWiretap(); // todo: setup later
        app.MapControllers();

        logger.LogDebug("Everything initialized. Starting up...");

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

internal static class Extensions
{
    public static IEnumerable<DateTimeOffset> ToLocalTime(this IEnumerable<DateTimeOffset> source, bool convert)
    {
        return source.Select(x => convert ? x.ToLocalTime() : x);
    }

    public static IAsyncEnumerable<DateTimeOffset> ToLocalTime(this IAsyncEnumerable<DateTimeOffset> source, bool convert)
    {
        return source.Select(x => convert ? x.ToLocalTime() : x);
    }
}

public record QuartzServerOptions
{
    public int StartDelaySeconds { get; init; }
}