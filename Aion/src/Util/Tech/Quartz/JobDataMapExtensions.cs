using System;
using System.Collections.Generic;
using Quartz;

namespace Aion.Util.Tech.Quartz;

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

    public static void PutEnum<T>(this JobDataMap map, T value) where T : Enum
    {
        map.Put(typeof(T).Name, value.ToString());
    }

    public static T GetEnum<T>(this JobDataMap map) where T : struct, Enum
    {
        return
            map.GetString(typeof(T).Name) is { } value
                ? Enum.Parse<T>(value)
                : throw new KeyNotFoundException($"Key '{typeof(T).Name}' not found in JobDataMap");
    }
}