using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Logging;
using Aion.Core.Options;
using Aion.Core.Workflows;
using Aion.Util;
using Aion.Util.Logging;
using Aion.Util.Quartz;
using Aion.Util.Templates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Commands;

public class RenderWorkflow
(
    ILogger<RenderWorkflow> logger,
    IOptions<SchedulerOptions> schedulerOptions
)
{
    public async Task<Workflow> For
    (
        WorkflowMatch workflowMatch,
        ITrigger? trigger = null,
        IImmutableList<StepIdentifier>? stepOrder = null,
        Func<string, Task<WorkflowTemplate>>? loadTemplate = null
    )
    {
        using var scope = logger.BeginScopeFrom(new { workflowMatch.WorkflowName });
        logger.LogTrace("Rendering workflow.");

        loadTemplate ??= WorkflowTemplate.FromFile;
        var template = await loadTemplate(workflowMatch.WorkflowPath);
        CronExpression.ValidateExpression(template.Cron);

        // meta: Create a partial workflow first so that we can use the Mode property for variables.
        var workflow = new Workflow
        {
            Profile = workflowMatch.Profile.Name,
            Enabled = template.Enabled,
            Name = workflowMatch.WorkflowName,
            Path = workflowMatch.WorkflowPath,
            Trigger = trigger ?? WorkflowTrigger.Create(workflowMatch.Profile.Name, workflowMatch.WorkflowName, template.Cron),
            Variables = template.Variables.ToImmutableDictionary(),
            //Logging = await template.Logging.Get(workflowMatch.Profile.LoggingPresets).Let(jsonObject => jsonObject.RenderFilePaths(variables)),
            //Steps = steps.ToImmutableList(),
        };

        var variables = ImmutableList<TemplateVariableGroup>.Empty.AddRange
        ([
            new GlobalVariableGroup(schedulerOptions.Value.Variables),
            new GlobalVariableGroup(workflowMatch.Profile.Variables),
            new GlobalVariableGroup(template.Variables),
            new SchedulerVariableGroup { Name = schedulerOptions.Value.Name },
            new ProfileVariableGroup
            {
                Name = workflowMatch.Profile.Name,
                Path = workflowMatch.Profile.Path,
            },
            new WorkflowVariableGroup
            {
                Name = workflowMatch.WorkflowName,
                Mode = workflow.Mode,
            }
        ]);

        // core: Merge profile and workflow environments with intended precedence: the workflow overrides profile.
        var environment =
            workflowMatch
                .Profile
                .Environment
                .ToImmutableDictionary()
                .SetItems(template.Environment)
                .ToImmutableDictionary(x => x.Key, x => x.Value.Render(variables));
        var stepTasks = template.Steps.Select((step, index) => RenderStep(step, index, workflowMatch.Profile.LoggingPresets, environment, variables));

        try
        {
            var steps = await Task.WhenAll(stepTasks);

            if (stepOrder is not null && stepOrder.Count > 0)
            {
                steps =
                    stepOrder
                        .Select(indexOrName => steps.First(s => indexOrName == s.Index || indexOrName == s.Name))
                        .ToArray();
            }

            // core: Workflows without any enabled steps are invalid.
            if (steps.Length == 0) throw new WorkflowNotExecutableException("Workflow has no executable steps.");

            logger.LogTrace("Steps rendered: {StepCount}", steps.Length);

            return workflow with
            {
                Logging = await template.Logging.Get(workflowMatch.Profile.LoggingPresets).Let(jsonObject => jsonObject.RenderFilePaths(variables)),
                Steps = steps.ToImmutableList(),
            };
        }
        catch (Exception ex)
        {
            // logger.LogError(ex, "Failed to render workflow template from '{WorkflowPath}'.", workflowMatch.Path);
            throw new WorkflowTemplateException(workflowMatch.WorkflowPath, ex);
        }
    }

    private async Task<Workflow.Step> RenderStep
    (
        WorkflowTemplate.StepTemplate template,
        int index,
        LoggingPresetRepository loggingPresets,
        IImmutableDictionary<string, string> environment,
        IImmutableList<TemplateVariableGroup> variables
    )
    {
        using var scope = logger.BeginScopeFrom(new { StepIndex = index });
        variables = variables.Add(new StepVariableGroup { Index = index, Name = template.Name });
        try
        {
            return new Workflow.Step
            {
                Index = index,
                Name = template.Name,
                Enabled = template.Enabled,
                FileName = template.FileName.Render(variables),
                Arguments = () => template.Arguments.Select(cla => cla with { Values = cla.Values?.Select(v => v.Render(variables)).ToArray() }),
                // core: Merge environment with intended precedence: the step overrides workflow.
                Environment = environment.SetItems(template.Environment),
                WorkingDirectory = (template.WorkingDirectory ?? string.Empty).Render(variables),
                Timeout = template.Timeout ?? System.Threading.Timeout.InfiniteTimeSpan,
                DependsOn = template.DependsOn,
                Logging = await template.Logging.Get(loggingPresets).Let(jsonObject => jsonObject.RenderFilePaths(variables)),
            }.Also(step =>
            {
                // meta: Rendering arguments requires an activity in scope.
                using var activity = new Activity("RenderingStepArguments").Start();
                // meta: Make sure that arguments are renderable before they are used.
                //step.Arguments();
            });
        }
        catch (Exception ex)
        {
            // logger.LogError(ex, "Failed to render step template at {StepIndex}.", index);
            throw new StepTemplateException(index, ex);
        }
    }
}

public class WorkflowNotExecutableException(string message) : Exception(message);

public class WorkflowTemplateException(string path, Exception innerException) : Exception
(
    message: $"Failed to render workflow template from '{path}'.",
    innerException: innerException
);

public class StepTemplateException(int index, Exception innerException) : Exception
(
    message: $"Failed to render step template at {index}.",
    innerException: innerException
);