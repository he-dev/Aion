using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Aion.Jobs;
using Aion.Services;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;

namespace Aion
{
    internal class Program
    {
        public const string Name = "Aion";
        public const string Version = "5.0.0";

        private static async Task Main(params string[] args)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            // var configuration =
            //     new ConfigurationBuilder()
            //         .SetBasePath(Directory.GetCurrentDirectory())
            //         .AddJsonFile("appsettings.json", optional: false)
            //         .AddEnvironmentVariables()
            //         .Build();

            using var host =
                Host
                    .CreateDefaultBuilder(args)
                    .ConfigureAppConfiguration(builder =>
                    {
                        builder
                            .SetBasePath(Directory.GetCurrentDirectory())
                            //.AddJsonFile("appsettings.json", optional: false)
                            .AddEnvironmentVariables();
                    })
                    .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                    .ConfigureServices((context, services) =>
                    {
                        var schedulerOptions = context.Configuration.GetSection(nameof(WorkflowScheduler));
                        services.AddQuartz(q =>
                        {
                            //q.UseMicrosoftDependencyInjectionJobFactory();
                            //q.ScheduleJob<WorkflowScheduler>(trigger => { trigger.WithIdentity(nameof(WorkflowScheduler)).WithCronSchedule(schedulerOptions["Schedule"]); });
                        });
                        //services.AddQuartzHostedService();
                        services.AddHostedService<CronService>();
                        //services.AddSingleton<CustomJobFactory>();
                        //services.AddSingleton(services =>  new StdSchedulerFactory(new NameValueCollection()));
                        //services.AddTransient<WorkflowScheduler>();
                        //services.AddTransient<RobotLauncher>();
                        services.Configure<WorkflowScheduler.Options>(context.Configuration.GetSection(nameof(WorkflowScheduler)));
                    }).ConfigureContainer<ContainerBuilder>(builder =>
                    {
                        builder.RegisterInstance(new StdSchedulerFactory(new NameValueCollection())).SingleInstance();
                        builder.RegisterType<CustomJobFactory>().SingleInstance();
                        //builder.RegisterType<WorkflowScheduler>();
                        //builder.RegisterType<RobotLauncher>();
                    })
                    .UseWindowsService(options => { options.ServiceName = $"{Name}-v{Version}"; })
                    .Build();

            await host.RunAsync();


            if (Environment.UserInteractive)
            {
                Console.ReadKey();
            }
        }
    }


    internal class CustomJobFactory : IJobFactory
    {
        private readonly ILogger<CustomJobFactory> _logger;
        private readonly ILifetimeScope _lifetimeScope;

        public CustomJobFactory(ILogger<CustomJobFactory> logger, ILifetimeScope lifetimeScope)
        {
            _logger = logger;
            _lifetimeScope = lifetimeScope;
        }

        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            using var scope = _lifetimeScope.BeginLifetimeScope(builder =>
            {
                builder.RegisterInstance(scheduler).As<IScheduler>();
                builder.RegisterType<WorkflowScheduler>();
                builder.RegisterType<WorkflowLauncher>();
            });

            return (IJob)scope.Resolve(bundle.JobDetail.JobType);
        }

        public void ReturnJob(IJob job)
        {
            (job as IDisposable)?.Dispose();
        }
    }
}