using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Options;
using Aion.Core.Workflows;
using Aion.Util;
using Aion.Util.Logging;
using Aion.Util.Templates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Commands.Workflows;

public class RenderWorkflow
(
    ILogger<RenderWorkflow> logger,
    IOptions<SchedulerOptions> schedulerOptions
)
{
    public async Task<Workflow> For
    (
        WorkflowPath workflowPath,
        ITrigger? trigger = null,
        IImmutableList<StepIdentifier>? stepOrder = null,
        Func<string, Task<WorkflowTemplate>>? loadTemplate = null
    )
    {
        using var scope = logger.BeginScopeFrom(new { workflowPath.WorkflowName });
        logger.LogTrace("Rendering workflow.");

        loadTemplate ??= WorkflowTemplate.FromFile;
        var template = await loadTemplate(workflowPath);
        CronExpression.ValidateExpression(template.Cron);

        var profile = schedulerOptions.Value.Profiles[workflowPath.ProfileName];

        // meta: Create a partial workflow first so that we can use the Mode property for variables.
        var workflow = new Workflow
        {
            Profile = workflowPath.ProfileName,
            Enabled = template.Enabled,
            Name = workflowPath.WorkflowName,
            Path = workflowPath.ToString(),
            Trigger = trigger ?? WorkflowTrigger.Create(workflowPath.ProfileName, workflowPath.WorkflowName, template.Cron),
            Variables = template.Variables.ToImmutableDictionary(),
        };

        var variables = ImmutableList<TemplateVariableGroup>.Empty.AddRange
        ([
            new GlobalVariableGroup(schedulerOptions.Value.Variables),
            new GlobalVariableGroup(profile.Variables),
            new GlobalVariableGroup(template.Variables),
            new SchedulerVariableGroup { Name = schedulerOptions.Value.Name },
            new ProfileVariableGroup
            {
                //Root = workflowPath.ProfileRoot,
                Name = workflowPath.ProfileName,
            },
            new WorkflowVariableGroup
            {
                Name = workflowPath.WorkflowName,
                Mode = workflow.Mode,
            }
        ]);

        // core: Merge profile and workflow environments with intended precedence: the workflow overrides profile.
        var environment =
            profile
                .Environment
                .ToImmutableDictionary()
                .SetItems(template.Environment)
                .ToImmutableDictionary(x => x.Key, x => x.Value.Render(variables));
        var stepTasks = template.Steps.Select((step, index) => RenderStep(profile, variables, environment, step, index));

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
                Logging = await profile.GetLoggingOrDefault(template.Logging).Let(jsonObject => jsonObject.RenderFilePaths(variables)),
                Steps = steps.ToImmutableList(),
            };
        }
        catch (Exception ex)
        {
            throw new RenderWorkflowException(workflowPath, ex);
        }
    }

    private async Task<Workflow.Step> RenderStep
    (
        Profile profile,
        IImmutableList<TemplateVariableGroup> variables,
        IImmutableDictionary<string, string> environment,
        WorkflowStepTemplate template,
        int index
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
                Arguments = () => template.Arguments.Select(argument => argument.RenderValues(variables)),
                // core: Merge environment with intended precedence: the step overrides workflow.
                Environment = environment.SetItems(template.Environment),
                WorkingDirectory = (template.WorkingDirectory ?? string.Empty).Render(variables),
                Timeout = template.Timeout ?? System.Threading.Timeout.InfiniteTimeSpan,
                DependsOn = template.DependsOn,
                Logging = await profile.GetLoggingOrDefault(template.Logging).Let(jsonObject => jsonObject.RenderFilePaths(variables)),
                LoggingTarget = template.Logging.Target,
            };
        }
        catch (Exception ex)
        {
            throw new RenderStepException(index, ex);
        }
    }
}

public class WorkflowNotExecutableException(string message) : Exception(message);

public class RenderWorkflowException(string path, Exception innerException) : Exception
(
    message: $"Failed to render workflow template from '{path}'.",
    innerException: innerException
);

public class RenderStepException(int index, Exception innerException) : Exception
(
    message: $"Failed to render step template at {index}.",
    innerException: innerException
);