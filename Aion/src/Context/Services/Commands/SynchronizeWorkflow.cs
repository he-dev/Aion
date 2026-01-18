using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aion.Modules;
using Aion.Modules.Services;
using Aion.Modules.Services.Queries;
using Aion.Toolbox.Logging;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Context.Services.Commands;

public class SynchronizeWorkflow
(
    ILogger<SynchronizeWorkflow> logger,
    GetProfile getProfile,
    CreateWorkflow createWorkflow,
    IEnumerable<ISynchronizeWorkflow> synchronizeWorkflows
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
        var profile = getProfile.Where(profileName);
        var workflowPath = profile.Workflows.Single(workflowName);
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

        foreach (var synchronizeWorkflow in synchronizeWorkflows)
        {
            try
            {
                if (await synchronizeWorkflow.Try(workflow) is { } result)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to synchronize workflow '{WorkflowName}'.", workflow.Name);
                throw new SynchronizeWorkflowException(workflow.Name, synchronizeWorkflow, ex);
            }
        }

        throw new Exception($"No workflow synchronization strategy could handle workflow '{workflow.Name}'.");
    }
}

public class SynchronizeWorkflowException(string workflowName, object action, Exception inner) :
    Exception($"Failed to synchronize workflow '{workflowName}' with action '{action.GetType().Name}': {inner.Message}", inner);