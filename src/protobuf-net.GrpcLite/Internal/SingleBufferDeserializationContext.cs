using Grpc.Core;
using System.Buffers;
using System.Runtime.InteropServices;

namespace ProtoBuf.Grpc.Lite.Internal;

internal sealed class SingleBufferDeserializationContext : DeserializationContext, IPooled
{
    private ReadOnlySequence<byte> _payload;

    public void Initialize(in ReadOnlySequence<byte> payload)
    {
        _payload = payload;
    }

    public void Recycle()
    {
        _payload = default;
        Pool<SingleBufferDeserializationContext>.Put(this);
    }

    public override byte[] PayloadAsNewBuffer()
    {
        var payload = _payload;
        if (payload.IsEmpty) return Utilities.EmptyBuffer;

        if (payload.IsSingleSegment)
        {
            var single = payload.First;
            if (MemoryMarshal.TryGetArray(single, out var segment)
                && segment.Offset == 0 && segment.Count == single.Length)
            {
                return segment.Array!;
            }
        }
        return payload.ToArray();
    }
    public override ReadOnlySequence<byte> PayloadAsReadOnlySequence()
        => _payload;

    public override int PayloadLength => checked((int)_payload.Length);
}
