using Quartz;

namespace Aion.Toolbox.Quartz;

public static class GroupName
{
    // Job.Profile.WorkflowMode
    //public static string For<T>(params string[] names) => new[] { typeof(T).Name }.Concat(names).Join(".");

    //public static string For<T>(string profileName, WorkflowMode mode)
    //{
    //    return For<T>(profileName, mode.ToString());
    //}
}

public record JobGroup<T>(string ProfileName) where T : IJob
{
    public static implicit operator string(JobGroup<T> group) => $"{typeof(T).Name}[{group.ProfileName}]";
}