using System.Threading.Tasks;
using Aion.Core.Workflows;
using Aion.Util;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Jobs;

[UsedImplicitly]
[DisallowConcurrentExecution]
internal class SynchronizationJob
(
    ILogger<SynchronizationJob> logger,
    WorkflowDirectory directory,
    WorkflowSchedule scheduler
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Synchronizing workflows.");
        await foreach (var result in directory)
        {
            switch (result)
            {
                case Result<Workflow, Workflow.Issue>.Success { Value: var workflow }:
                    await scheduler.Synchronize(workflow);
                    break;
                case Result<Workflow, Workflow.Issue>.Failure { Value: var workflowIssue }:
                    // !! Just ignore them.
                    break;
            }
        }
    }

    public static IJobDetail CreateJobDetail() =>
        JobBuilder
            .Create<SynchronizationJob>()
            .WithIdentity("workflow-synchronization", JobGroupNames.Services)
            .Build();
}