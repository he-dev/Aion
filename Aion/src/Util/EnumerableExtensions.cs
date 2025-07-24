using System;
using System.Collections.Generic;

namespace Aion.Util;

public static class EnumerableExtensions
{
    public static T SingleOrThrows<T>(this IEnumerable<T> source)
    {
        using var enumerator = source.GetEnumerator();

        // meta: Try to advance to the first item.
        if (!enumerator.MoveNext())
        {
            throw new CollectionEmptyException();
        }

        var first = enumerator.Current;

        // meta: Try to advance to the second item.
        if (enumerator.MoveNext())
        {
            // Multiple matches.
            throw new AmbiguousResultException();
        }

        return first;
    }
}

// $"Multiple workflows match filter '{fileNameFilter}': {string.Join(',', fileNames)}."

public class CollectionEmptyException : Exception;

public class AmbiguousResultException : Exception;