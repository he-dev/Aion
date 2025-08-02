using System;
using System.Collections.Generic;

namespace Aion.Util;

public static class EnumerableExtensions
{
    /// <summary>
    /// Tries to get the only element of a collection by throwing more specific exceptions that the built-in Single in case it was not possible.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="onEmpty"></param>
    /// <param name="onExtra"></param>
    public static T SingleOrThrows<T>(this IEnumerable<T> source, Func<Exception> onEmpty, Func<Exception> onExtra)
    {
        using var enumerator = source.GetEnumerator();

        // core: Try to advance to the first item.
        if (!enumerator.MoveNext())
        {
            throw onEmpty();
        }

        var first = enumerator.Current;

        // core: Try to advance to the second item.
        if (enumerator.MoveNext())
        {
            // Multiple matches.
            throw onExtra();
        }

        return first;
    }
}

public class CollectionEmptyException : Exception;

public class AmbiguousResultException : Exception;