using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Aion.Util;

public abstract record Result<TSuccess, TFailure>
{
    public record Success(TSuccess Value) : Result<TSuccess, TFailure>;

    public record Failure(TFailure Value) : Result<TSuccess, TFailure>;
}

public static class ResultExtensions
{
    public static async Task<(IImmutableList<TSuccess> SuccessResults, IImmutableList<TFailure> FailureResults)> ClusterAsync<TSuccess, TFailure>
    (
        this IAsyncEnumerable<Result<TSuccess, TFailure>> source
    )
    {
        var successResults = ImmutableList<TSuccess>.Empty;
        var failureResults = ImmutableList<TFailure>.Empty;

        await foreach (var result in source)
        {
            switch (result)
            {
                case Result<TSuccess, TFailure>.Success success:
                    successResults = successResults.Add(success.Value);
                    break;
                case Result<TSuccess, TFailure>.Failure failure:
                    failureResults = failureResults.Add(failure.Value);
                    break;
            }
        }

        return (successResults, failureResults);
    }

    public static async IAsyncEnumerable<TSuccess> SuccessOnlyAsync<TSuccess, TFailure>
    (
        this IAsyncEnumerable<Result<TSuccess, TFailure>> source
    )
    {
        await foreach (var either in source)
        {
            if (either is Result<TSuccess, TFailure>.Success x)
            {
                yield return x.Value;
            }
        }
    }

    public static IEnumerable<TSuccess> SuccessOnly<TSuccess, TFailure>
    (
        this IEnumerable<Result<TSuccess, TFailure>> source
    )
    {
        foreach (var either in source)
        {
            if (either is Result<TSuccess, TFailure>.Success x)
            {
                yield return x.Value;
            }
        }
    }

    public static async IAsyncEnumerable<TFailure> FailureOnlyAsync<TSuccess, TFailure>
    (
        this IAsyncEnumerable<Result<TSuccess, TFailure>> source
    )
    {
        await foreach (var either in source)
        {
            if (either is Result<TSuccess, TFailure>.Failure x)
            {
                yield return x.Value;
            }
        }
    }
}