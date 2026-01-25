using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Aion.Util;
using Aion.Util.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Services.Workflows;

public class SynchronizeWorkflow
(
    ILogger<SynchronizeWorkflow> logger,
    GetProfile getProfile,
    FindWorkflows findWorkflows,
    CreateWorkflow createWorkflow,
    IEnumerable<SynchronizeWorkflowAction> synchronizeWorkflowActions
)
{
    // https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

    public async Task<SynchronizeWorkflowResult> Invoke
    (
        string profileName,
        string workflowName,
        ITrigger trigger,
        IImmutableList<StepIdentifier>? stepOrder = null
    )
    {
        var profile = getProfile.Single(profileName);
        var workflowPath = findWorkflows.Single(WorkflowSearchCriteria.Where(profile, workflowName));
        return await Invoke(workflowPath, trigger, stepOrder);
    }

    public async Task<SynchronizeWorkflowResult> Invoke
    (
        WorkflowPath workflowPath,
        ITrigger? trigger = null,
        IImmutableList<StepIdentifier>? stepOrder = null
    )
    {
        var workflowTemplate = await WorkflowConfiguration.FromFile(workflowPath);
        return await Invoke(workflowTemplate, trigger, stepOrder);
    }

    // note: This can only succeed, otherwise it throws an exception.
    public async Task<SynchronizeWorkflowResult> Invoke
    (
        WorkflowConfiguration workflowConfiguration,
        ITrigger? trigger = null,
        IImmutableList<StepIdentifier>? stepOrder = null
    )
    {
        var workflow = await createWorkflow.From(workflowConfiguration, trigger, stepOrder);
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflow.Name });

        foreach (var synchronizeWorkflow in synchronizeWorkflowActions)
        {
            try
            {
                if (await synchronizeWorkflow.Invoke(workflow).ToListAsync() is { Count: > 0 } steps)
                {
                    return new SynchronizeWorkflowResult(workflow.Name)
                    {
                        Action = synchronizeWorkflow.GetType().Name,
                        Steps = steps
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to synchronize workflow '{WorkflowName}'.", workflow.Name);
                return new SynchronizeWorkflowResult(workflow.Name)
                {
                    Action = synchronizeWorkflow.GetType().Name,
                    Error = ex.Message
                };
            }
        }

        logger.LogInformation("No workflow synchronization strategy applies to workflow '{WorkflowName}'.", workflow.Name);
        return new SynchronizeWorkflowResult(workflow.Name);
    }
}

public record SynchronizeWorkflowResult(string WorkflowName)
{
    public string? Action { get; init; }

    public IEnumerable<SynchronizationStep> Steps { get; init; } = [];

    public string? Error { get; init; }
}
