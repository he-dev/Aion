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
    /// <param name="onAmbiguous"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="CollectionEmptyException">The collection has no elements at all.</exception>
    /// <exception cref="AmbiguousResultException">The collection has more than one element.</exception>
    public static T SingleOrThrows<T>(this IEnumerable<T> source, Func<Exception> onEmpty, Func<Exception> onAmbiguous)
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
            throw onAmbiguous();
        }

        return first;
    }
}

public class CollectionEmptyException : Exception;

public class AmbiguousResultException : Exception;