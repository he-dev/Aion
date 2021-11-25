using System;
using Aion.Jobs;
using Autofac;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Spi;
using Reusable.Extensions;

namespace Aion.DependencyInjection;

internal class AutofacJobFactory : IJobFactory
{
    private readonly ILogger<AutofacJobFactory> _logger;
    private readonly ILifetimeScope _lifetimeScope;

    public AutofacJobFactory(ILogger<AutofacJobFactory> logger, ILifetimeScope lifetimeScope)
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

        try
        {
            _logger.LogDebug("Creating job: '{}'", bundle.JobDetail.JobType.ToPrettyString());
            return (IJob)scope.Resolve(bundle.JobDetail.JobType);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error creating job.", ex);
            throw;
        }
    }

    public void ReturnJob(IJob job)
    {
        (job as IDisposable)?.Dispose();
    }
}