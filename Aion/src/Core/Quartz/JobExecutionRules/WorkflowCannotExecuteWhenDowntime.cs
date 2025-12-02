using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Options;
using Aion.Core.Workflows;
using Aion.Util.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Quartz.JobExecutionRules;

public class WorkflowCannotExecuteWhenDowntime
(
    ILogger<WorkflowCannotExecuteWhenDowntime> logger,
    IOptions<SchedulerOptions> schedulerOptions
) : ITriggerListener
{
    public string Name => nameof(WorkflowCannotExecuteWhenDowntime);

    public async Task<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var profileName = trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var workflowName = trigger.JobDataMap.GetString(JobDataKeys.WorkflowName)!;

        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName, WorkflowName = workflowName });

        var profile = schedulerOptions.Value.Profiles[profileName];
        try
        {
            var workflowMatch = profile.Workflows.Single(workflowName);
            if (await WorkflowDowntime.FromFile(workflowMatch.Path) is { } workflowDowntime)
            {
                if (workflowDowntime.Status == WorkflowDowntimeStatus.Expired)
                {
                    logger.LogWarning("Workflow downtime has expired on {ExpiresOn} and will be deleted.", workflowDowntime.EndsOnUtc.ToLocalTime());
                    await workflowDowntime.EndsNow();
                }

                return workflowDowntime.Status == WorkflowDowntimeStatus.Ongoing;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow downtime could not be checked.");

            try
            {
                if (ex is WorkflowDowntimeIssue issue)
                {
                    File.Delete(issue.Path);
                }
            }
            catch (Exception inner)
            {
                logger.LogError(inner, "Workflow downtime could not be cleaned up.");
            }

            // core: Use the fail-close principle. If not sure whether it's open, then it's closed.
            return true;
        }

        return false;
    }

    public Task TriggerFired(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScopeFrom(new { ProfileName = trigger.JobDataMap.GetString(JobDataKeys.ProfileName) });
        logger.LogDebug("Workflow execution trigger '{TriggerName}' has fired.", trigger.Key.Name);
        return Task.CompletedTask;
    }

    public Task TriggerMisfired(ITrigger trigger, CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScopeFrom(new { ProfileName = trigger.JobDataMap.GetString(JobDataKeys.ProfileName) });
        logger.LogWarning("Workflow execution trigger '{TriggerName}' has misfired.", trigger.Key.Name);
        return Task.CompletedTask;
    }

    public Task TriggerComplete(ITrigger trigger, IJobExecutionContext context, SchedulerInstruction triggerInstructionCode, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}