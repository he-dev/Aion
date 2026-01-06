using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aion.Core.Options;
using Aion.Core.Workflows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Core.Commands.Workflows;

public class ScheduleWorkflow
(
    ILogger<ScheduleWorkflow> logger,
    IOptionsSnapshot<SchedulerOptions> schedulerOptions,
    RenderWorkflow renderWorkflow,
    WorkflowScheduleRegistry workflowScheduleRegistry
)
{
    public async Task<DateTimeOffset> Invoke
    (
        string profileName,
        string workflowName,
        DateTimeOffset? startAtUtc,
        IImmutableList<StepIdentifier>? stepOrder = null
    )
    {
        try
        {
            var profile = schedulerOptions.Value.Profiles[profileName];
            var workflowMatch = profile.Workflows.Single(workflowName);
            var trigger = WorkflowTrigger.Create(profileName, workflowMatch.WorkflowName, startAtUtc!.Value);
            var workflow = await renderWorkflow.For(workflowMatch, trigger, stepOrder);
            var result = await workflowScheduleRegistry.AddOrUpdate(workflow);
            return result.NextUtc!.Value.ToLocalTime();
        }
        catch (NoWorkflowMatch)
        {
            //return NotFound($"No workflow matches the name '{workflowName}'.");
        }
        catch (AmbiguousWorkflowMatch)
        {
            //return BadRequest($"Multiple workflows match the name '{workflowName}'.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to schedule workflow '{WorkflowName}'.", workflowName);
            // return Problem(
            //     detail: ex.ToString(),
            //     title: $"Unable to schedule workflow '{workflowName}'.",
            //     statusCode: 500,
            //     instance: Request.Path
            // );
        }

        return DateTimeOffset.MinValue;
    }
}