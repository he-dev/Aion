using System;
using System.Collections.Generic;

namespace AionApi.Util;

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

