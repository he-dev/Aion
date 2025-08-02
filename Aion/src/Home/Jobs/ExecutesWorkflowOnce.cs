using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services;
using Aion.Meta.Logging;
using Aion.Util.Quartz;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Home.Jobs;

public class ExecutesWorkflowOnce
(
    ILogger<ExecutesWorkflowOnce> logger,
    IOptions<EngineOptions> engineOptions,
    ExecutesWorkflow executesWorkflow
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var profileName = context.Trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var workflowName = context.Trigger.JobDataMap.GetString(JobDataKeys.WorkflowName)!;
        var workflowStart = context.Trigger.JobDataMap.GetEnum<WorkflowStart>();
        var profile = engineOptions.Value[profileName];
        using var activity = new Activity($"ExecutingWorkflow{workflowStart}").Start();
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName, WorkflowName = workflowName, WorkflowStart = workflowStart });

        try
        {
            var workflowMatch = await profile.WorkflowMatch(workflowName).Load();
            switch (workflowMatch.Value)
            {
                // util: Logging.
                case { Steps: { } steps } when steps.Any(s => s.IsOn) == false:
                    logger.LogWarning("Skipping workflow because it has no enabled steps.");
                    break;
                // core: This is where the actual magic happens.
                case var workflow:
                    await executesWorkflow.Now(workflowMatch);
                    break;
            }
            activity.SetStatus(ActivityStatusCode.Ok).Stop();
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            logger.LogError(ex, "Error executing workflow.");
        }
    }
}