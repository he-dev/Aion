using System;
using System.Collections.Generic;
using System.Linq;

namespace Aion.Core;

internal static class DateTimeOffsetExtensions
{
    public static IEnumerable<DateTimeOffset> ToLocalTime(this IEnumerable<DateTimeOffset> source, bool convert)
    {
        return source.Select(x => convert ? x.ToLocalTime() : x);
    }

    public static IAsyncEnumerable<DateTimeOffset> ToLocalTime(this IAsyncEnumerable<DateTimeOffset> source, bool convert)
    {
        return source.Select(x => convert ? x.ToLocalTime() : x);
    }
}