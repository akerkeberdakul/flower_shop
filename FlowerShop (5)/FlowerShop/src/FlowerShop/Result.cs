namespace FlowerShop;

public abstract class Result<T, TError>
{
    private Result() { }

    public abstract bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public abstract TOut Match<TOut>(Func<T, TOut> onSuccess, Func<TError, TOut> onFailure);

    public static Result<T, TError> Ok(T value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value), "A successful result must carry a value.");
        }

        return new SuccessCase(value);
    }

    public static Result<T, TError> Fail(TError error)
    {
        if (error is null)
        {
            throw new ArgumentNullException(nameof(error), "A failed result must carry an error.");
        }

        return new FailureCase(error);
    }

    private sealed class SuccessCase : Result<T, TError>
    {
        private readonly T _value;

        public SuccessCase(T value) { _value = value; }

        public override bool IsSuccess => true;

        public override TOut Match<TOut>(Func<T, TOut> onSuccess, Func<TError, TOut> onFailure)
        {
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onFailure);
            return onSuccess(_value);
        }
    }

    private sealed class FailureCase : Result<T, TError>
    {
        private readonly TError _error;

        public FailureCase(TError error) { _error = error; }

        public override bool IsSuccess => false;

        public override TOut Match<TOut>(Func<T, TOut> onSuccess, Func<TError, TOut> onFailure)
        {
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onFailure);
            return onFailure(_error);
        }
    }
}

public static class ResultExtensions
{
    public static Result<TOut, TError> Map<T, TOut, TError>(this Result<T, TError> result, Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(map);

        return result.Match(
            value => Result<TOut, TError>.Ok(map(value)),
            error => Result<TOut, TError>.Fail(error));
    }

    public static Result<TOut, TError> Bind<T, TOut, TError>(this Result<T, TError> result, Func<T, Result<TOut, TError>> bind)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(bind);

        return result.Match(
            value => bind(value) ?? throw new InvalidOperationException("The Bind function returned null instead of a Result."),
            error => Result<TOut, TError>.Fail(error));
    }
}
