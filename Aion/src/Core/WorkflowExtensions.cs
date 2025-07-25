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
    public static JobKey CreatesJobKey(this Workflow workflow, string profile) => new(workflow.Name, new GroupName<ExecutesWorkflowOnSchedule>(profile));

    // note: Catch this property as it might throw when the Cron property is invalid.
    public static ICronTrigger CreatesCronTrigger(this Workflow workflow, string profile) =>
        (ICronTrigger)TriggerBuilder
            .Create()
            .WithIdentity(workflow.Name, new GroupName<ExecutesWorkflowOnSchedule>(profile))
            .UsingJobData(nameof(Workflow.Path), workflow.Path)
            .UsingJobData(nameof(ProfileInfo), profile)
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