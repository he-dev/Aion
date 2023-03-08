using System;
using System.Collections.Generic;
using Quartz;

namespace AionApi.Utilities;

public static class Generator
{
    public static IEnumerable<TItem> Generate<TSource, TItem>(this TSource source, Func<TSource, TItem> first, Func<TSource, TItem, TItem> next)
    {
        var previous = first(source);
        yield return previous;

        while (true)
        {
            var current = next(source, previous);
            yield return current;
            previous = current;
        }
        // ReSharper disable once IteratorNeverReturns - This generator is by design infinite.
    }
}


public static class StringExtensions
{
    public static CronExpression ToCronExpression(this string source) => new(source);
}
