namespace FlowerShop;

public abstract class Maybe<T>
{
    private Maybe() { }

    public static Maybe<T> None { get; } = new NoneCase();

    public static Maybe<T> Some(T value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value), "Use Maybe.None for an absent value.");
        }

        return new SomeCase(value);
    }

    public bool IsSome => this is SomeCase;

    public bool IsNone => !IsSome;

    public abstract TOut Match<TOut>(Func<T, TOut> onSome, Func<TOut> onNone);

    private sealed class SomeCase : Maybe<T>
    {
        private readonly T _value;

        public SomeCase(T value) { _value = value; }

        public override TOut Match<TOut>(Func<T, TOut> onSome, Func<TOut> onNone)
        {
            ArgumentNullException.ThrowIfNull(onSome);
            ArgumentNullException.ThrowIfNull(onNone);
            return onSome(_value);
        }
    }

    private sealed class NoneCase : Maybe<T>
    {
        public override TOut Match<TOut>(Func<T, TOut> onSome, Func<TOut> onNone)
        {
            ArgumentNullException.ThrowIfNull(onSome);
            ArgumentNullException.ThrowIfNull(onNone);
            return onNone();
        }
    }
}

public static class MaybeExtensions
{
    public static Result<T, TError> ToResult<T, TError>(this Maybe<T> maybe, TError error)
    {
        ArgumentNullException.ThrowIfNull(maybe);

        return maybe.Match(
            value => Result<T, TError>.Ok(value),
            () => Result<T, TError>.Fail(error));
    }
}
