using System.Linq;
using Quartz;

namespace Aion.Util.Quartz;

public class GroupName<T>(params string[] names) where T : IJob
{
    public override string ToString() => new[] { typeof(T).Name }.Concat(names).Join(":");

    public static implicit operator string(GroupName<T> jobGroupName) => jobGroupName.ToString();
}