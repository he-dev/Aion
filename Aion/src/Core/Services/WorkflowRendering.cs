using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Entities;
using Aion.Core.Services.Jobs;
using Aion.Util.Entities.Quartz;
using Aion.Util.Quartz;
using Aion.Util.Services;
using Quartz;

namespace Aion.Core.Services;

public static class WorkflowRendering
{
    public static async Task<Workflow> ToWorkflow
    (
        this WorkflowMatch workflowMatch,
        IImmutableList<TemplateVariableGroup> variables,
        Func<string, Task<WorkflowTemplate>>? loadTemplate = null
    )
    {
        loadTemplate ??= WorkflowTemplate.FromFile;
        var template = await loadTemplate(workflowMatch.Path);

        variables = variables.Add(new WorkflowVariableGroup(template.Variables)
        {
            Name = workflowMatch.Name,
        });

        var environment = workflowMatch.Profile.Environment.ToImmutableDictionary().AddRange(template.Environment);
        var stepTasks = template.Steps.Select((step, index) => CreateStep(step, index, workflowMatch.Profile.LoggingPresets, environment, variables));
        var steps = await Task.WhenAll(stepTasks);

        return new Workflow
        {
            Name = new WorkflowName(workflowMatch.PathWithinProfile),
            Path = workflowMatch.Path,
            Enabled = template.Enabled,
            CreateTrigger = (startOnceAtUtc) =>
            {
                var executionMode = startOnceAtUtc is null ? WorkflowExecutionMode.Cron : WorkflowExecutionMode.Once;
                var group = GroupName.For<WorkflowExecutionJob>(workflowMatch.Profile.Name, executionMode);

                var triggerBuilder =
                    TriggerBuilder
                        .Create()
                        .ForJob(nameof(WorkflowExecutionJob), group)
                        .WithIdentity(workflowMatch.Name, group)
                        .UsingJobData(JobDataKeys.WorkflowName, workflowMatch.Name)
                        .UsingJobData(JobDataKeys.ProfileName, workflowMatch.Profile.Name)
                        .UsingJobData(executionMode);

                // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault - There are only these two cases.
                switch (executionMode)
                {
                    case WorkflowExecutionMode.Cron:
                        triggerBuilder.WithCronSchedule(template.Cron);
                        break;
                    case WorkflowExecutionMode.Once:
                        triggerBuilder.StartAt(startOnceAtUtc!.Value);
                        triggerBuilder.WithSimpleSchedule(x => x.WithRepeatCount(0));
                        break;
                }

                return triggerBuilder.Build();
            },
            Variables = template.Variables.ToImmutableDictionary(),
            Logging = await template.Logging.OrPreset(workflowMatch.Profile.LoggingPresets).Let(jsonObject => jsonObject.RenderFilePaths(variables)),
            Steps = steps.ToImmutableList(),
        };
    }

    private static async Task<Workflow.Step> CreateStep
    (
        WorkflowTemplate.StepTemplate template,
        int index,
        LoggingPresetRepository loggingPresets,
        IImmutableDictionary<string, string> environment,
        IImmutableList<TemplateVariableGroup> variables
    )
    {
        variables = variables.Add(new StepVariableGroup { Index = index, Name = template.Name });
        return new Workflow.Step
        {
            Index = index,
            Name = template.Name,
            Enabled = template.Enabled,
            FileName = template.FileName.Render(variables),
            Arguments = () => (template.Arguments ?? string.Empty).Render(variables),
            Environment = template.Environment.ToImmutableDictionary().AddRange(environment),
            WorkingDirectory = (template.WorkingDirectory ?? string.Empty).Render(variables),
            Timeout = template.Timeout ?? System.Threading.Timeout.InfiniteTimeSpan,
            DependsOn = template.DependsOn,
            Logging = await template.Logging.OrPreset(loggingPresets).Let(jsonObject => jsonObject.RenderFilePaths(variables)),
        };
    }

    // meta: There are requests that require a workflow for informational purposes, but without the actual execution and variables.
    public static async Task<Workflow> ToWorkflowDraft
    (
        this WorkflowMatch workflowMatch,
        Func<string, Task<WorkflowTemplate>>? loadTemplate = null
    )
    {
        var variables = ImmutableList<TemplateVariableGroup>.Empty.AddRange
        ([
            new InstanceVariableGroup([]) { Name = "Draft" },
            new ProfileVariableGroup([]) { Name = "Draft" },
            new ExecutionVariableGroup { Mode = WorkflowExecutionMode.None },
        ]);

        return await workflowMatch.ToWorkflow(variables, loadTemplate);
    }
}