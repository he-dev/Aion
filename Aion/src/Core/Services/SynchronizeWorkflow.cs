using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Aion.Util;
using Aion.Util.Services;
using Aion.Util.Services.Synchronizations;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Services;

public class SynchronizeWorkflow
(
    ILogger<SynchronizeWorkflow> logger,
    GetProfile getProfile,
    FindWorkflows findWorkflows,
    CreateWorkflow createWorkflow,
    IEnumerable<ISynchronizeWorkflow> synchronizeWorkflowActions
)
{
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

        foreach (var synchronizeWorkflowAction in synchronizeWorkflowActions)
        {
            try
            {
                if (await synchronizeWorkflowAction.Try(workflow) is { } result)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to synchronize workflow '{WorkflowName}'.", workflow.Name);
                return new SynchronizeWorkflowResult(workflow.Name, synchronizeWorkflowAction.GetType()) { Exception = ex };
            }
        }

        throw new Exception($"No workflow synchronization strategy could handle workflow '{workflow.Name}'.");
    }
}

public class SynchronizeWorkflowException(string workflowName, object action, Exception inner) :
    Exception($"Failed to synchronize workflow '{workflowName}' with action '{action.GetType().Name}': {inner.Message}", inner);