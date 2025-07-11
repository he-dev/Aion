using System;
using System.Collections.Generic;
using System.Linq;

namespace Aion.Util;

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

    // util: Quartz.net requires UTC for some APIs, but some requests specify the local time without the offset, so it needs to be fixed.
    // note: Assumes local time.
    public static DateTimeOffset FixMissingOffset(this DateTimeOffset value)
    {
        return new DateTimeOffset(value.DateTime, TimeZoneInfo.Local.GetUtcOffset(value));
    }
}