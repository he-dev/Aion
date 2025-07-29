using System.Linq;
using Quartz;

namespace Aion.Util.Quartz;

public static class JobGroupName
{
    public static string From<T>(params string[] names) => new[] { typeof(T).Name }.Concat(names).Join(".");
}