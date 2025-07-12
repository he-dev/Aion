using System;
using System.CommandLine;
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

        var builder = CreateHostBuilder(args);


        var app = builder.Build();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Everything initialized in {Elapsed} seconds. Starting up...", stopwatch.Elapsed);

        await app.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        var disableSyncOption = new Option<bool>("--app-disable-sync", "Disable the sync job.") { IsRequired = false };
        var rootCommand = new RootCommand { disableSyncOption };
        var commandLine = rootCommand.Parse(args);

        return Host
            .CreateDefaultBuilder(args)
            // .ConfigureAppConfiguration((context, builder) =>
            // {
            //     builder
            //         .AddJsonFile("appsettings.json", optional: false)
            //         .AddCommandLine(source =>
            //         {
            //             source.Args = args;
            //
            //         });
            // })
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

                // .. There's no way these settings are missing so suppress the null warnings.

                services.AddSingleton(x => x.GetRequiredService<IHostEnvironment>().ContentRootFileProvider);
                services.AddSingleton<WorkflowScheduler>();
                services.AddSingleton<WorkflowScheduler.Collection>();
                services.AddSingleton<WorkflowProcess>();
                services.AddSingleton<WorkflowDirectory>();
                services.AddSingleton<WorkflowMaintenance>();

                services.AddScoped<RegularWorkflowJob>();
                services.AddScoped<OnDemandWorkflowJob>();
                services.AddScoped<SynchronizationJob>();

                services.AddQuartz(q =>
                {
                    var synchronizationJobOptions = context.Configuration.GetRequiredSection("SynchronizationJob").Get<SynchronizationJobOptions>()!;
                    var jobDetail = SynchronizationJob.CreateJobDetail();

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
                            .StartNow()
                            .WithSimpleSchedule(x => x.WithRepeatCount(0));
                    });
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