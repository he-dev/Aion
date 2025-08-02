using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services.Scheduling;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Home.Jobs;

[DisallowConcurrentExecution]
internal class SynchronizesWorkflows
(
    ILogger<SynchronizesWorkflows> logger,
    IOptions<EngineOptions> engineOptions,
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

        var matches = profile.Workflows();
        foreach (var match in matches)
        {
            try
            {
                await schedulesWorkflowCron.For(match);
                logger.LogInformation("Workflow '{WorkflowPath}' has been scheduled.", match.Path);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load '{WorkflowPath}'.", match.Path);
            }
        }
    }
}
