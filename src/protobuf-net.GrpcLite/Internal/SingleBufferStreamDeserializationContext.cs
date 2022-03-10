using Grpc.Core;
using System.Buffers;

namespace ProtoBuf.Grpc.Lite.Internal;

internal sealed class SingleBufferStreamDeserializationContext : DeserializationContext
{
    private static SingleBufferStreamDeserializationContext? _spare;
    private byte[] _buffer = Array.Empty<byte>();
    private int _offset, _length;

    public static SingleBufferStreamDeserializationContext Get()
        => Interlocked.Exchange(ref _spare, null) ?? new SingleBufferStreamDeserializationContext();

    private SingleBufferStreamDeserializationContext Reset()
    {
        _offset = _length = 0;
        _buffer = Array.Empty<byte>();
        return this;
    }

    public void Initialize(byte[] buffer, int offset, int length)
    {
        _buffer = buffer;
        _offset = offset;
        _length = length;
    }

    public void Recycle() => _spare = Reset();

    public override byte[] PayloadAsNewBuffer()
    {
        if (_offset == 0 && _length == _buffer.Length) return _buffer;
        var copy = _length == 0 ? Array.Empty<byte>() : new byte[_length];
        Buffer.BlockCopy(_buffer, _offset, copy, 0, _length);
        return copy;
    }
    public override ReadOnlySequence<byte> PayloadAsReadOnlySequence()
        => new ReadOnlySequence<byte>(_buffer, _offset, _length);

    public override int PayloadLength => throw new NotImplementedException();
}
