using System.Linq;
using Aion.Core.Entities;

namespace Aion.Util.Entities.Quartz;

public static class GroupName
{
    public static string For<T>(params string[] names) => new[] { typeof(T).Name }.Concat(names).Join(".");

    public static string For<T>(string profileName, WorkflowExecutionMode executionMode)
    {
        return For<T>(profileName, executionMode.ToString());
    }
}