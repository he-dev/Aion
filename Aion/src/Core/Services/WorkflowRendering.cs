using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Entities;
using Aion.Core.Services.Jobs;
using Aion.Util.Entities.Quartz;
using Aion.Util.Logging;
using Aion.Util.Quartz;
using Aion.Util.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Services;

public class WorkflowRendering
(
    ILogger<WorkflowRendering> logger,
    IOptions<InstanceOptions> instanceOptions
)
{
    public async Task<Workflow> RenderFrom
    (
        WorkflowMatch workflowMatch,
        DateTimeOffset? startOnceAtUtc = null,
        Func<string, Task<WorkflowTemplate>>? loadTemplate = null
    )
    {
        var executionMode = startOnceAtUtc is null ? WorkflowExecutionMode.Cron : WorkflowExecutionMode.Once;

        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflowMatch.Name });
        logger.LogTrace("Rendering workflow for '{ExecutionMode}' mode.", executionMode);

        loadTemplate ??= WorkflowTemplate.FromFile;
        var template = await loadTemplate(workflowMatch.Path);

        var variables = ImmutableList<TemplateVariableGroup>.Empty.AddRange
        ([
            new InstanceVariableGroup(instanceOptions.Value.Variables) { Name = instanceOptions.Value.Name },
            new ProfileVariableGroup(workflowMatch.Profile.Variables) { Name = workflowMatch.Profile.Name },
            new ExecutionVariableGroup { Mode = executionMode },
            new WorkflowVariableGroup(template.Variables) { Name = workflowMatch.Name }
        ]);

        var environment = workflowMatch.Profile.Environment.ToImmutableDictionary().SetItems(template.Environment);
        var stepTasks = template.Steps.Select((step, index) => RenderStep(step, index, workflowMatch.Profile.LoggingPresets, environment, variables));

        try
        {
            var steps = await Task.WhenAll(stepTasks);
            logger.LogTrace("Steps rendered: {StepCount}", steps.Length);

            return new Workflow
            {
                Name = new WorkflowName(workflowMatch.PathWithinProfile),
                Path = workflowMatch.Path,
                Enabled = template.Enabled,
                CreateTrigger = () =>
                {
                    var group = GroupName.For<WorkflowExecutionJob>(workflowMatch.Profile.Name, executionMode);

                    var triggerBuilder =
                        TriggerBuilder
                            .Create()
                            .ForJob(nameof(WorkflowExecutionJob), group)
                            .WithIdentity(workflowMatch.Name, group)
                            .UsingJobData(JobDataKeys.WorkflowName, workflowMatch.Name)
                            .UsingJobData(JobDataKeys.ProfileName, workflowMatch.Profile.Name)
                            .UsingJobData(executionMode);

                    switch (executionMode)
                    {
                        case WorkflowExecutionMode.Cron:
                            CronExpression.ValidateExpression(template.Cron);
                            triggerBuilder.WithCronSchedule(template.Cron);
                            break;
                        case WorkflowExecutionMode.Once:
                            triggerBuilder.StartAt(startOnceAtUtc!.Value);
                            triggerBuilder.WithSimpleSchedule(x => x.WithRepeatCount(0));
                            break;
                        default:
                            // meta: This will never happen, but makes the compiler happy.
                            throw new ArgumentOutOfRangeException();
                    }

                    return triggerBuilder.Build();
                },
                Variables = template.Variables.ToImmutableDictionary(),
                Logging = await template.Logging.OrPreset(workflowMatch.Profile.LoggingPresets).Let(jsonObject => jsonObject.RenderFilePaths(variables)),
                Steps = steps.ToImmutableList(),
            };
        }
        catch (Exception ex)
        {
            // logger.LogError(ex, "Failed to render workflow template from '{WorkflowPath}'.", workflowMatch.Path);
            throw new WorkflowTemplateException(workflowMatch.Path, ex);
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
                Arguments = () => (template.Arguments ?? string.Empty).Render(variables),
                // core: Merge environment with intended precedence: the step overrides workflow.
                Environment = environment.SetItems(template.Environment),
                WorkingDirectory = (template.WorkingDirectory ?? string.Empty).Render(variables),
                Timeout = template.Timeout ?? System.Threading.Timeout.InfiniteTimeSpan,
                DependsOn = template.DependsOn,
                Logging = await template.Logging.OrPreset(loggingPresets).Let(jsonObject => jsonObject.RenderFilePaths(variables)),
            }.Also(step =>
            {
                // meta: Rendering arguments requires an activity in scope.
                using var activity = new Activity("RenderingStepArguments").Start();
                // meta: Make sure that arguments are renderable before they are used.
                step.Arguments();
            });
        }
        catch (Exception ex)
        {
            // logger.LogError(ex, "Failed to render step template at {StepIndex}.", index);
            throw new StepTemplateException(index, ex);
        }
    }
}

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