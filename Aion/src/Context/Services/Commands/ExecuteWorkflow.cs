using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Context.Jobs;
using Aion.Modules;
using Aion.Modules.Logging;
using Aion.Modules.Scheduler;
using Aion.Modules.Services;
using Aion.Toolbox.Logging;
using Aion.Toolbox.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Context.Services.Commands;

public class ExecuteWorkflow
(
    ILogger<WorkflowJob> logger,
    IOptions<SchedulerOptions> schedulerOptions,
    MapLogEvent mapLogEvent,
    CreateWorkflow createWorkflow,
    ExecuteStep executeStep
)
{
    public async Task<StepResultCollection> Now
    (
        string profileName,
        string workflowName,
        IImmutableList<StepIdentifier>? stepOrder = null
    )
    {
        var profile = schedulerOptions.Value.Profiles[profileName];
        var workflowPath = profile.Workflows.Single(workflowName);
        var workflowTemplate = await WorkflowConfiguration.FromFile(workflowPath);
        var workflow = await createWorkflow.From(workflowTemplate, CreateTrigger.Simple(profileName, workflowName, DateTimeOffset.UtcNow), stepOrder: stepOrder);
        return await Now(workflow);
    }

    public async Task<StepResultCollection> Now(Workflow workflow)
    {
        if (workflow is { Mode: WorkflowMode.Cron, Enabled: false })
        {
            return [];
        }

        using var activity = new Activity(nameof(ExecuteWorkflow)).Start();
        using var scope = logger.BeginScopeFrom(new
        {
            ProfileName = workflow.Profile,
            WorkflowName = workflow.Name,
            WorkflowMode = workflow.Mode,
        });

        using var executionSignature = new WorkflowSignatureScope(logger);
        using var logging = mapLogEvent.By(executionSignature, to: workflow.Logging.ToLogger());

        logger.LogInformation("Executing workflow: '{WorkflowName}'", workflow.Name);

        // core: Does not filter out disabled steps because we want them logged.
        var stepResults = new StepResultCollection();
        foreach (var step in workflow.Steps)
        {
            var stepResult = await executeStep.Now(step);
            stepResults.Add(stepResult);

            if (stepResult.ExitCode is not null and not 0 && step.OnError is { } onError)
            {
                if (onError.Trim().Equals("continue", StringComparison.OrdinalIgnoreCase))
                {
                    // core: Just continue with the next step.
                }

                if (onError.Trim().Equals("break", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Workflow execution stopped due to a failed step.");
                    break;
                }
            }
        }

        activity.SetStatus(ActivityStatusCode.Ok).Stop();

        logger.LogInformation("Workflow '{WorkflowName}' completed in {Duration:N0} ms.", workflow.Name, activity.Duration);
        logger.LogInformation
        (
            "Steps={TotalStepCount}, Executed={ExecutedStepCount}, Passed={PassedStepCount}, Failed={FailedStepCount}.",
            stepResults.Count,
            stepResults.ExecutedStepCount,
            stepResults.PassedStepCount,
            stepResults.FailedStepCount
        );

        return stepResults;
    }
}

public class StepResultCollection : Collection<StepResult>
{
    public int ExecutedStepCount => this.Count(result => result.ExitCode is not null);
    public int PassedStepCount => this.Count(result => result.ExitCode == 0);
    public int FailedStepCount => this.Count(result => result.ExitCode is not null && result.ExitCode != 0);
}