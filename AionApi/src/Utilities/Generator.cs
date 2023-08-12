using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Quartz;

namespace AionApi.Utilities;

public static class Generator
{
    public static IEnumerable<TItem> Generate<TSource, TItem>(this TSource source, Func<TSource, TItem?> first, Func<TSource, TItem, TItem> next)
    {
        var previous = first(source);
        if (previous is null) yield break;

        yield return previous;

        while (true)
        {
            if (next(source, previous) is { } current)
            {
                yield return current;
                previous = current;
            }
            else
            {
                yield break;
            }
        }
        // ReSharper disable once IteratorNeverReturns - This generator is by design infinite.
    }
}

public static class StringExtensions
{
    public static CronExpression ToCronExpression(this string source) => new(source);
}