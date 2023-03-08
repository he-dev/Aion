using System;
using System.Threading.Tasks;
using AionApi.Jobs;
using AionApi.Models;
using AionApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Reusable.Wiretap.Abstractions;
using Reusable.Wiretap.Channels;
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

        builder.Services.AddSingleton<ILogger>(_ => LoggerBuilder.CreateDefault().Use<LogToConsole>().Use<LogToNLog>().Build());

        var workflowEngineSection = builder.Configuration.GetSection("WorkflowEngine");

        builder.Services.Configure<WorkflowEngineOptions>(workflowEngineSection);
        builder.Services.AddSingleton(services => services.GetRequiredService<IHostEnvironment>().ContentRootFileProvider);
        builder.Services.AddSingleton<WorkflowStore>();
        builder.Services.AddScoped<Services.WorkflowScheduler>();
        builder.Services.AddScoped<WorkflowRunner>();

        builder.Services.AddQuartz(q =>
        {
            q.UseMicrosoftDependencyInjectionJobFactory();
            q.ScheduleJob<Jobs.WorkflowScheduler>(trigger =>
            {
                var engineOptions = new WorkflowEngineOptions();
                workflowEngineSection.Bind(engineOptions);
                trigger
                    .WithIdentity("aion-schedule-workflows", nameof(Workflow))
                    .WithCronSchedule(CronScheduleBuilder.CronSchedule(engineOptions.UpdaterSchedule))
                    .StartAt(DateTimeOffset.UtcNow.AddSeconds(engineOptions.UpdaterStartDelay));
            });
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
        app.MapControllers();
        await app.RunAsync();
    }
}