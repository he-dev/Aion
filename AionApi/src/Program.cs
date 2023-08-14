using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AionApi.Jobs;
using AionApi.Models;
using AionApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Quartz;
using Reusable;
using Reusable.Extensions;
using Reusable.IO;
using Reusable.IO.Abstractions;
using Reusable.Wiretap;
using Reusable.Wiretap.Abstractions;
using Reusable.Wiretap.AspNetCore;
using Reusable.Wiretap.Modules;
using Reusable.Wiretap.Modules.Loggers;
using Reusable.Wiretap.Services;

namespace AionApi;

public static class Program
{
    public static async Task Main(params string[] args)
    {
        NLog.LogManager.LoadConfiguration("NLog.config");

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddSingleton<LogAction>(_ => LogActionBuilder.CreateDefault().Use<LogToConsole>().Use<LogToNLog>().Build());
        builder.Services.AddSingleton(typeof(LoggerFactory));
        builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        builder.Services.AddWiretap();

        var engineOptions = new WorkflowEngineOptions();
        var workflowEngineSection = builder.Configuration.GetSection("WorkflowEngine").Also(section => section.Bind(engineOptions));

        builder.Services.Configure<WorkflowEngineOptions>(workflowEngineSection);
        builder.Services.AddSingleton(services => services.GetRequiredService<IHostEnvironment>().ContentRootFileProvider);
        builder.Services.AddSingleton<WorkflowStore>();
        builder.Services.AddSingleton<WorkflowProcess>();
        builder.Services.AddScoped<WorkflowScheduler>();
        builder.Services.AddScoped<WorkflowLauncher>();
        builder.Services.AddSingleton<IAsyncProcess, AsyncProcess>();
        builder.Services.AddSingleton<IDirectoryTree, DirectoryTree>();
        builder.Services.AddSingleton<WorkflowDirectory>();

        builder.Services.AddQuartz(q =>
        {
            q.UseMicrosoftDependencyInjectionJobFactory();
            if (engineOptions.UpdaterEnabled)
            {
                q.ScheduleJob<ScheduleUpdater>(trigger =>
                {
                    trigger
                        .WithIdentity("update-workflows", JobGroupNames.Services)
                        .WithCronSchedule(CronScheduleBuilder.CronSchedule(engineOptions.UpdaterSchedule))
                        .StartAt(DateTimeOffset.UtcNow.AddSeconds(engineOptions.UpdaterStartDelay));
                });
            }
        });
        builder.Services.AddQuartzServer(options =>
        {
            options.AwaitApplicationStarted = true;
            options.WaitForJobsToComplete = true;
            options.StartDelay = TimeSpan.FromSeconds(10);
        });
        //builder.Services.AddHostedService<>()

        var app = builder.Build();
        //var scheduler = await app.Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
        //scheduler.ScheduleJob<WorkflowInitializer>(t => )

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.UseWiretap();
        app.MapControllers();
        await app.RunAsync();
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