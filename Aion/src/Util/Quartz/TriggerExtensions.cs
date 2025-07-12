using System;
using System.Collections.Generic;
using Quartz;

namespace Aion.Util.Quartz;

public static class TriggerExtensions
{
    public static IEnumerable<DateTimeOffset?> FiresAt(this ITrigger trigger, DateTimeOffset afterTimeUtc)
    {
        while (true)
        {
            if (trigger.GetFireTimeAfter(afterTimeUtc) is { } nextFireTimeUtc)
            {
                yield return nextFireTimeUtc;
                afterTimeUtc = nextFireTimeUtc;
            }
            else
            {
                yield break;
            }
        }
    }
}

public static class JobDataMapExtensions
{
    public static T Get<T>(this JobDataMap data, string key)
    {
        if (data.TryGetValue(key, out var value))
        {
            if (value is null)
            {
                throw new NullReferenceException($"Key {key} has a null value.");
            }

            return (T)value;
        }

        throw new KeyNotFoundException($"Key '{key}' not found in JobDataMap");
    }
}