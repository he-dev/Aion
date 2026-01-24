using System;
using Quartz;

namespace Aion.Meta.Quartz;

public static class TriggerBuilderExtensions
{
    public static TriggerBuilder UsingJobData<T>(this TriggerBuilder builder, T value) where T : Enum
    {
        return builder.UsingJobData(typeof(T).Name, value.ToString());
    }
}