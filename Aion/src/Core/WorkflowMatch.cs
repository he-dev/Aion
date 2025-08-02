using System;
using System.Threading.Tasks;
using Aion.Core.Services;
using Aion.Home.Jobs;
using Aion.Util.Quartz;
using Quartz;

namespace Aion.Core;

public class WorkflowMatch
{
    private Workflow? _workflow;

    public WorkflowMatch(Profile profile, string relativePath)
    {
        Profile = profile;
        RelativePath = relativePath;

        // core: Ensure the workflow name is valid.
        Name.EnsureUrlSafe();
    }

    public Profile Profile { get; init; }

    public string RelativePath { get; init; }

    public string Path => System.IO.Path.Join(Profile.Path, RelativePath);

    public string Name => System.IO.Path.GetFileNameWithoutExtension(RelativePath).Replace(System.IO.Path.DirectorySeparatorChar, '.');

    public Workflow Value => _workflow ?? throw new InvalidOperationException("Workflow has not been loaded yet.");

    public async Task<WorkflowMatch> Load()
    {
        _workflow = await Workflow.FromFile(Path);
        _workflow.EnsureCronSchedulable();
        await _workflow.EnsureTemplatesRenderable(Profile);
        return this;
    }

    public JobKey CronJobKey => new(Name, JobGroupName.From<ExecutesWorkflowCron>(Profile.Name));

    // note: Catch this property as it might throw when the Cron property is invalid.
    public ICronTrigger CronTrigger =>
        (ICronTrigger)TriggerBuilder
            .Create()
            .WithIdentity(Name, JobGroupName.From<ExecutesWorkflowCron>(Profile.Name))
            .UsingJobData(JobDataKeys.WorkflowName, Name)
            .UsingJobData(JobDataKeys.ProfileName, Profile.Name)
            .UsingJobData(WorkflowStart.Cron)
            .WithCronSchedule(Value.Cron) //, x => x.InTimeZone(Value.TimeZone))
            .Build();
}