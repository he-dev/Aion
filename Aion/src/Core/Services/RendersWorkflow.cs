using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Home.Jobs;
using Aion.Util.Quartz;
using Aion.Util.Scriban;
using Quartz;

namespace Aion.Core.Services;

public static class RendersWorkflow
{
    public static async Task<Workflow> From(WorkflowMatch workflowMatch, IImmutableList<VariableGroup> variables)
    {
        var template = await workflowMatch.Load();

        variables = variables.Add(new WorkflowVariableGroup(template.Variables?.ToImmutableDictionary())
        {
            Name = workflowMatch.Name,
        });

        var stepTasks = template.Steps.Select((step, index) => CreateStep(step, index, workflowMatch.Profile, variables));
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
                    .WithCronSchedule(template.Cron) //, x => x.InTimeZone(Value.TimeZone))
                    .Build(),
            Variables = template.Variables?.ToImmutableDictionary() ?? ImmutableDictionary<string, string>.Empty,
            Environment = template.Environment?.ToImmutableDictionary() ?? ImmutableDictionary<string, string>.Empty,
            Logging = await RendersLogging.From(template.Logging, workflowMatch.Profile.LoggingPresets, variables),
            Steps = steps.ToImmutableList(),
        };
    }

    private static async Task<Workflow.Step> CreateStep(WorkflowTemplate.StepTemplate template, int index, Profile profile, IImmutableList<VariableGroup> variables)
    {
        variables = variables.Add(new StepVariableGroup { Index = index, Name = template.Name });
        return new Workflow.Step
        {
            Index = index,
            Name = template.Name,
            Enabled = template.Enabled,
            FileName = RendersTemplates.In(template.FileName, variables),
            Arguments = () => RendersTemplates.In(template.Arguments ?? string.Empty, variables),
            Environment = template.Environment?.ToImmutableDictionary() ?? ImmutableDictionary<string, string>.Empty,
            WorkingDirectory = RendersTemplates.In(template.WorkingDirectory ?? string.Empty, variables),
            Timeout = template.Timeout ?? System.Threading.Timeout.InfiniteTimeSpan,
            DependsOn = template.DependsOn,
            Logging = await RendersLogging.From(template.Logging, profile.LoggingPresets, variables)
        };
    }
}