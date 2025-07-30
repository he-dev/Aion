using System.Collections.Immutable;
using System.Linq;
using Aion.Home.Jobs;
using Aion.Util.Json;
using Aion.Util.Quartz;
using Aion.Util.Scriban;
using Quartz;

namespace Aion.Core;

public static class ExtendsWorkflow
{
    public static JobKey CreatesJobKey(this Workflow workflow, string profileName) => new(workflow.Name, JobGroupName.From<ExecutesWorkflowCron>(profileName));

    // note: Catch this property as it might throw when the Cron property is invalid.
    public static ICronTrigger CreatesCronTrigger(this Workflow workflow, string profileName) =>
        (ICronTrigger)TriggerBuilder
            .Create()
            .WithIdentity(workflow.Name, JobGroupName.From<ExecutesWorkflowCron>(profileName))
            .UsingJobData(JobDataKeys.WorkflowName, workflow.Name)
            .UsingJobData(JobDataKeys.ProfileName, profileName)
            .UsingJobData(WorkflowTriggerType.Cron)
            .WithCronSchedule(workflow.Cron, x => x.InTimeZone(workflow.TimeZone))
            .Build();

    public static Workflow.Step RenderTemplates(this Workflow.Step step, IImmutableList<VariableGroup> variables)
    {
        return step with
        {
            File = RendersTemplates.In(step.File, variables),
            Args = step.Args.Select(arg => RendersTemplates.In(arg, variables)).ToList(),
            WorkingDirectory = RendersTemplates.In(step.WorkingDirectory ?? string.Empty, variables),
            Logging = step.Logging.RenderFilePaths(template => RendersTemplates.In(template, variables))
        };
    }
}