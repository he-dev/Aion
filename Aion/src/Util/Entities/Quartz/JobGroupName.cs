using System.Linq;
using Aion.Core.Entities;

namespace Aion.Util.Entities.Quartz;

public static class JobGroupName
{
    public static string From<T>(params string[] names) => new[] { typeof(T).Name }.Concat(names).Join(".");

    public static string From<T>(WorkflowExecutionMode executionMode) => new[] { typeof(T).Name }.Append(executionMode.ToString()).Join(".");
}