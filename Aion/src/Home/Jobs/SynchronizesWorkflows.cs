using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services;
using Aion.Core.Services.Scheduling;
using Aion.Meta.Logging;
using Aion.Util.Scriban;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Home.Jobs;

[DisallowConcurrentExecution]
internal class SynchronizesWorkflows
(
    ILogger<SynchronizesWorkflows> logger,
    IOptions<InstanceOptions> engineOptions,
    SchedulesWorkflowCron schedulesWorkflowCron
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var profileName = context.Trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var profile = engineOptions.Value[profileName];
        using var activity = new Activity("SynchronizingWorkflows").Start();
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        logger.LogInformation("Scheduling profile...");

        var matches = profile.Workflows.All();
        foreach (var match in matches)
        {
            try
            {
                var workflow = await RendersWorkflow.From(match, ImmutableList<VariableGroup>.Empty);
                await schedulesWorkflowCron.For(workflow);
                logger.LogInformation("Workflow '{WorkflowPath}' has been scheduled.", match.Path);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load '{WorkflowPath}'.", match.Path);
            }
        }
    }
}
