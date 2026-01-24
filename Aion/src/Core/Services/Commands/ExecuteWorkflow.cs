using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Jobs;
using Aion.Meta.Logging;
using Aion.Meta.Serilog;
using Aion.Util;
using Aion.Util.Logging;
using Aion.Util.Services;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Services.Commands;

public class ExecuteWorkflow
(
    ILogger<WorkflowJob> logger,
    MapLogEvent mapLogEvent,
    CreateWorkflow createWorkflow,
    GetProfile getProfile,
    FindWorkflows findWorkflows,
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
        var profile = getProfile.Single(profileName);
        var workflowPath = findWorkflows.Single(WorkflowSearchCriteria.Where(profile, workflowName));
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
        try
        {
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

            logger.LogInformation(
                "Workflow '{WorkflowName}' completed in {Duration:N0} ms. Steps: {Total}, Passed: {Passed}, Failed={Failed}.",
                workflow.Name, activity.Duration, stepResults.Count, stepResults.Passed, stepResults.Failed
            );

            return stepResults;
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            logger.LogError(ex, "Workflow '{WorkflowName}' execution failed in {Duration:N0}.", workflow.Name, activity.Duration);
            throw;
        }
    }
}

public class StepResultCollection : Collection<StepResult>
{
    public int Actual => this.Count(result => result.ExitCode is not null);
    public int Passed => this.Count(result => result.ExitCode == 0);
    public int Failed => this.Count(result => result.ExitCode is not null && result.ExitCode != 0);
}