namespace ProtoBuf.Grpc.Lite.Internal;

internal readonly struct Maybe<T>
{
    internal Maybe(T value)
    {
        HasValue = true;
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
