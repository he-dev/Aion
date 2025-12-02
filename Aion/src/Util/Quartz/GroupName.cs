using System.Linq;
using Aion.Core.Workflows;

namespace Aion.Util.Quartz;

public static class GroupName
{
    public static string For<T>(params string[] names) => new[] { typeof(T).Name }.Concat(names).Join(".");

    public static string For<T>(string profileName, WorkflowMode mode)
    {
        return For<T>(profileName, mode.ToString());
    }
}