using System.Collections.Generic;
using System.Linq;

namespace Aion.Util;

public abstract record Either<TInL, TInR>
{
    public record InL(TInL Value) : Either<TInL, TInR>;

    public record InR(TInR Value) : Either<TInL, TInR>;
}

public static class EitherExtensions
{
    public static (IEnumerable<TLeft> Lefts, IEnumerable<TRight> Rights) Cluster<TLeft, TRight>
    (
        this IEnumerable<Either<TLeft, TRight>> source
    )
    {
        return
        (
            source.OfType<Either<TLeft, TRight>.InL>().Select(l => l.Value),
            source.OfType<Either<TLeft, TRight>.InR>().Select(r => r.Value)
        );

    }

    public static async IAsyncEnumerable<TLeft> InLsAsync<TLeft, TRight>(this IAsyncEnumerable<Either<TLeft, TRight>> source)
    {
        await foreach(var either in source)
        {
            if (either is Either<TLeft, TRight>.InL x)
            {
                yield return x.Value;
            }
        }
    }

    public static async IAsyncEnumerable<TInR> InRsAsync<TInL, TInR>(this IAsyncEnumerable<Either<TInL, TInR>> source)
    {
        await foreach(var either in source)
        {
            if (either is Either<TInL, TInR>.InR x)
            {
                yield return x.Value;
            }
        }
    }
}