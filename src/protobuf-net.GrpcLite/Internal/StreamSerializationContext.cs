using Grpc.Core;
using System.Buffers;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal;

internal sealed class StreamSerializationContext : SerializationContext, IBufferWriter<byte>, IPooled
{
    private readonly Queue<(byte[] Buffer, int Offset, int Length, bool ViaWriter)> _buffers = new();
    private byte[] _currentBuffer = Utilities.EmptyBuffer;
    private int _offset, _remaining, _nextSize = InitialBufferSize;
    private long _totalLength;
    const int InitialBufferSize = 1024, MaxBufferSize = 64 * 1024;

    public long Length => _totalLength;

    public async ValueTask<int> WritePayloadAsync(ChannelWriter<StreamFrame> writer, IHandler handler, bool isLastElement, CancellationToken cancellationToken)
    {
        var finalFlags = isLastElement ? (PayloadFlags.EndItem | PayloadFlags.EndAllItems) : PayloadFlags.EndItem;
        var frames = 0;
        if (_buffers.TryDequeue(out var buffer))
        {
            do
            {
                int remaining = buffer.Length, offset = buffer.Offset;
                while (remaining > 0)
                {
                    var take = Math.Min(remaining, ushort.MaxValue);

                    remaining -= take;
                    var payloadFlags = remaining == 0 && _buffers.Count == 0 ? finalFlags : PayloadFlags.None;
                    var frameFlags = buffer.ViaWriter ? FrameFlags.RecycleBuffer | FrameFlags.HeaderReserved : FrameFlags.None;
                    await writer.WriteAsync(new StreamFrame(FrameKind.Payload, handler.Id, (byte)payloadFlags, buffer.Buffer, buffer.Offset, (ushort)take, frameFlags, handler.NextSequenceId()), cancellationToken);
                    frames++;
                    offset += take;
                }
            }
            while (_buffers.TryDequeue(out buffer));
        }

        if (frames == 0)
        {
            // write an empty final payload if nothing was written
            await writer.WriteAsync(new StreamFrame(FrameKind.Payload, handler.Id, (byte)finalFlags), cancellationToken);
            frames++;
        }
        return frames;
    }
    public void Recycle()
    {
        _buffers.Clear();
        _totalLength = _offset = _remaining = 0;
        _nextSize = InitialBufferSize;
        if (_currentBuffer.Length != 0)
            ArrayPool<byte>.Shared.Return(_currentBuffer);
        _currentBuffer = Utilities.EmptyBuffer;
        Pool<StreamSerializationContext>.Put(this);
    }

    public StreamSerializationContext() { }

    public override IBufferWriter<byte> GetBufferWriter() => this;

    public override void Complete(byte[] payload)
    {
        _totalLength += payload.Length;
        _buffers.Enqueue((payload, 0, payload.Length, false));
    }

    public override void Complete() => Flush(false);

    //private int Available => _active.Length - _activeCommitted;
    private void Flush(bool getNew)
    {
        var written = _offset - StreamFrame.HeaderBytes;
        if (written > 0)
        {
            _buffers.Enqueue((_currentBuffer, StreamFrame.HeaderBytes, written, true));
            _totalLength += written;
        }
        if (getNew)
        {
            var size = _nextSize;
            if (size < MaxBufferSize) _nextSize <<= 1; // use incrementally bigger buffers, up to a limit
            _currentBuffer = ArrayPool<byte>.Shared.Rent(size);
            _offset = StreamFrame.HeaderBytes;
            _remaining = _currentBuffer.Length - StreamFrame.HeaderBytes;
        }
        else
        {
            _currentBuffer = Utilities.EmptyBuffer;
            _remaining = _offset = 0;
        }
    }

    void IBufferWriter<byte>.Advance(int count)
    {
        if (count < 0 || count > _remaining) Throw(count, _remaining);
        _offset += count;
        _remaining -= count;

        static void Throw(int count, int _remaining) => throw new ArgumentOutOfRangeException(nameof(count),
            $"Advance must be called with count in the range [0, {_remaining}], but {count} was specified");
    }

    Memory<byte> IBufferWriter<byte>.GetMemory(int sizeHint)
    {
        if (Math.Max(sizeHint, 64) > _remaining) Flush(true);
        return new Memory<byte>(_currentBuffer, _offset, _remaining);
    }

    Span<byte> IBufferWriter<byte>.GetSpan(int sizeHint)
    {
        if (Math.Max(sizeHint, 64) > _remaining) Flush(true);
        return new Span<byte>(_currentBuffer, _offset, _remaining);
    }
}
