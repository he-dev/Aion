using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Entities;
using Aion.Home.Jobs;
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

        variables = variables.Add(new WorkflowVariableGroup(template.Variables?.ToImmutableDictionary())
        {
            Name = workflowMatch.Name,
        });

        var environment = workflowMatch.Profile.Environment.ToImmutableDictionary().AddRange(template.Environment);
        var stepTasks = template.Steps.Select((step, index) => CreateStep(step, index, workflowMatch.Profile.LoggingPresets, environment, variables));
        var steps = await Task.WhenAll(stepTasks);

        return new Workflow
        {
            Name = new WorkflowName(workflowMatch.PathWithinProfile),
            Enabled = template.Enabled,
            CronJobKey = new JobKey(workflowMatch.Name, JobGroupName.From<ExecutesWorkflowCron>(workflowMatch.Profile.Name)),
            CronTrigger =
                (ICronTrigger)TriggerBuilder
                    .Create()
                    .WithIdentity(workflowMatch.Name, JobGroupName.From<ExecutesWorkflowCron>(workflowMatch.Profile.Name))
                    .UsingJobData(JobDataKeys.WorkflowName, workflowMatch.Name)
                    .UsingJobData(JobDataKeys.ProfileName, workflowMatch.Profile.Name)
                    .UsingJobData(WorkflowStart.Cron)
                    .UsingJobData(WorkflowExecutionMode.Cron)
                    // note: This will throw if the cron expression is invalid.
                    .WithCronSchedule(template.Cron) //, x => x.InTimeZone(Value.TimeZone))
                    .Build(),
            OnceTrigger = startAtUtc =>
            {
                var triggerBuilder =
                    TriggerBuilder
                        .Create()
                        .WithIdentity(workflowMatch.Name, JobGroupName.From<ExecutesWorkflowOnce>(workflowMatch.Profile.Name))
                        .UsingJobData(JobDataKeys.ProfileName, workflowMatch.Profile.Name)
                        .UsingJobData(JobDataKeys.WorkflowName, workflowMatch.Name)
                        .UsingJobData(WorkflowExecutionMode.Once)
                        .WithSimpleSchedule(x => x.WithRepeatCount(0));

                if (startAtUtc is null)
                {
                    triggerBuilder.StartNow();
                }
                else
                {
                    triggerBuilder.StartAt(startAtUtc.Value);
                }

                return triggerBuilder.Build();
            },
            Variables = template.Variables?.ToImmutableDictionary() ?? ImmutableDictionary<string, string>.Empty,
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
}