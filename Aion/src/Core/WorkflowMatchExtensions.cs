using Aion.Home.Jobs;
using Aion.Util.Quartz;
using Quartz;

namespace Aion.Core;

public static class WorkflowMatchExtensions
{
    // public static JobKey CronJobKey(this WorkflowMatch match) => new(match.WorkflowName, JobGroupName.From<ExecutesWorkflowCron>(match.Profile.Name));
    //
    // // note: Catch this property as it might throw when the Cron property is invalid.
    // public static ICronTrigger CronTrigger(this WorkflowMatch match) =>
    //     (ICronTrigger)TriggerBuilder
    //         .Create()
    //         .WithIdentity(match.WorkflowName, JobGroupName.From<ExecutesWorkflowCron>(match.Profile.Name))
    //         .UsingJobData(JobDataKeys.WorkflowName, match.WorkflowName)
    //         .UsingJobData(JobDataKeys.ProfileName, match.Profile.Name)
    //         .UsingJobData(WorkflowStart.Cron)
    //         .WithCronSchedule(match.Value.Cron, x => x.InTimeZone(match.Value.TimeZone))
    //         .Build();
}