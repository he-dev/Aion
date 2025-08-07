using System;
using System.IO;
using System.Threading.Tasks;
using Aion.Core.Services;
using Aion.Home.Jobs;
using Aion.Util.Quartz;
using Quartz;

namespace Aion.Core;

public class WorkflowMatch(Profile profile, string pathWithinProfile)
{
    private Workflow? _workflow;

    public Profile Profile => profile;

    public string PathWithinProfile => pathWithinProfile;

    public string Path => System.IO.Path.Join(Profile.Path, PathWithinProfile);

    public WorkflowName Name => new(PathWithinProfile);

    public Workflow Value => _workflow ?? throw new InvalidOperationException("Workflow has not been loaded yet.");

    // meta: The parameter makes testing easy.
    public async Task<WorkflowMatch> Load(Workflow? workflow = null)
    {
        _workflow = workflow ?? await Workflow.FromFile(Path);
        _workflow.EnsureCronSchedulable();
        await _workflow.EnsureTemplatesRenderable(Profile);
        return this;
    }

    // meta: Creating workflow-matches for tests is easier this way.
    internal static async Task<WorkflowMatch> Fake(Profile profile, string pathWithinProfile, Workflow workflow)
    {
        return await new WorkflowMatch(profile, pathWithinProfile).Load(workflow);
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

public record WorkflowName(string PathWithinProfile)
{
    public bool IsUrlSafe => Render().IsUrlSafe();

    private string Render() => Path.ChangeExtension(PathWithinProfile, null).Replace(Path.DirectorySeparatorChar, '.');

    public override string ToString() => Render().EnsureUrlSafe();

    public static implicit operator string(WorkflowName workflowName) => workflowName.ToString();
}