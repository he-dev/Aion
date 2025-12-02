using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Logging;
using Aion.Core.Options;
using Aion.Core.Quartz;
using Aion.Core.Quartz.Jobs;
using Aion.Core.Workflows;
using Aion.Home.Endpoints;
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
        DateTimeOffset? startOnceAtUtc = null,
        IImmutableList<StepIdentifier>? stepOrder = null,
        Func<string, Task<WorkflowTemplate>>? loadTemplate = null
    )
    {
        var workflowMode = startOnceAtUtc is null ? WorkflowMode.Cron : WorkflowMode.User;

        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflowMatch.Name });
        logger.LogTrace("Rendering workflow.");

        loadTemplate ??= WorkflowTemplate.FromFile;
        var template = await loadTemplate(workflowMatch.Path);
        CronExpression.ValidateExpression(template.Cron);

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
                Name = workflowMatch.Name,
                Mode = workflowMode,
            }
        ]);

        // core: Merge profile and workflow environments with intended precedence: the workflow overrides profile.
        var environment = workflowMatch.Profile.Environment.ToImmutableDictionary().SetItems(template.Environment);
        var stepTasks = template.Steps.Select((step, index) => RenderStep(step, index, workflowMatch.Profile.LoggingPresets, environment, variables));

        try
        {
            var steps = await Task.WhenAll(stepTasks);

            if (stepOrder is not null)
            {
                steps =
                    stepOrder
                        .Select(indexOrName => steps.First(s => indexOrName == s.Index || indexOrName == s.Name))
                        .ToArray();
            }

            // core: Workflows without any enabled steps are invalid.
            if(steps.Length == 0) throw new WorkflowNotExecutableException("Workflow has no executable steps.");

            logger.LogTrace("Steps rendered: {StepCount}", steps.Length);

            return new Workflow
            {
                Profile = workflowMatch.Profile.Name,
                Enabled = template.Enabled,
                Mode = workflowMode,
                Name = new WorkflowName(workflowMatch.PathWithinProfile),
                Path = workflowMatch.Path,
                CreateTrigger = () =>
                {
                    var group = GroupName.For<WorkflowJob>(workflowMatch.Profile.Name, workflowMode);

                    var triggerBuilder =
                        TriggerBuilder
                            .Create()
                            .ForJob(nameof(WorkflowJob), group)
                            .WithIdentity(workflowMatch.Name, group)
                            .UsingJobData(JobDataKeys.WorkflowName, workflowMatch.Name)
                            .UsingJobData(JobDataKeys.ProfileName, workflowMatch.Profile.Name)
                            .UsingJobData(workflowMode);

                    if (startOnceAtUtc is null)
                    {
                        triggerBuilder.WithCronSchedule(template.Cron);
                    }
                    else
                    {
                        triggerBuilder.StartAt(startOnceAtUtc!.Value);
                        triggerBuilder.WithSimpleSchedule(x => x.WithRepeatCount(0));
                    }

                    return triggerBuilder.Build();
                },
                Variables = template.Variables.ToImmutableDictionary(),
                Logging = await template.Logging.Get(workflowMatch.Profile.LoggingPresets).Let(jsonObject => jsonObject.RenderFilePaths(variables)),
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