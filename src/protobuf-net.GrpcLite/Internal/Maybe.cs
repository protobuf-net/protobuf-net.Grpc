namespace ProtoBuf.Grpc.Lite.Internal;

internal readonly struct Maybe<T>
{
    internal Maybe(bool hasValue, T value)
    {
        HasValue = hasValue;
        Value = value;
    }
    public bool HasValue { get; }
    public T Value { get; }

    public bool TryGetValue(out T value)
    {
        value = Value;
        return HasValue;
    }
}
