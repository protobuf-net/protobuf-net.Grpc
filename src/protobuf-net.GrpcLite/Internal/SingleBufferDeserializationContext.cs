using Grpc.Core;
using System.Buffers;

namespace ProtoBuf.Grpc.Lite.Internal;

internal sealed class SingleBufferDeserializationContext : DeserializationContext, IPooled
{
    private byte[] _buffer = Utilities.EmptyBuffer;
    private int _offset, _length;

    public void Initialize(byte[] buffer, int offset, int length)
    {
        _buffer = buffer;
        _offset = offset;
        _length = length;
    }

    public void Recycle()
    {
        _offset = _length = 0;
        _buffer = Utilities.EmptyBuffer;
        Pool<SingleBufferDeserializationContext>.Put(this);
    }

    public override byte[] PayloadAsNewBuffer()
    {
        if (_offset == 0 && _length == _buffer.Length) return _buffer;
        var copy = _length == 0 ? Utilities.EmptyBuffer : new byte[_length];
        Buffer.BlockCopy(_buffer, _offset, copy, 0, _length);
        return copy;
    }
    public override ReadOnlySequence<byte> PayloadAsReadOnlySequence()
        => new ReadOnlySequence<byte>(_buffer, _offset, _length);

    public override int PayloadLength => _length;
}
