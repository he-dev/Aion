namespace AionApi.Util;

public abstract record Either<TLeft, TRight>
{
    public record Left(TLeft Value) : Either<TLeft, TRight>;

    public record Right(TRight Value) : Either<TLeft, TRight>;
}