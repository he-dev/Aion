using System;
using System.Diagnostics;
using NLog;

namespace Aion.Util.NLog;

public class StopwatchConverter(IJsonConverter defaultConverter) : IJsonConverter
{
    public bool SerializeObject(object? value, System.Text.StringBuilder builder)
    {
        if (value is Stopwatch stopwatch)
        {
            builder.Append(Math.Round(stopwatch.Elapsed.TotalSeconds, 1));
            return true;
        }


        return defaultConverter.SerializeObject(value, builder);
    }
}