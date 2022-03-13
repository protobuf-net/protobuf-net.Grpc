using Grpc.Core;
using System.Buffers;

namespace ProtoBuf.Grpc.Lite.Internal;

internal sealed class FrameSerializationContext : SerializationContext, IBufferWriter<byte>, IPooled
{
    private readonly Queue<BufferSegment> _buffers = new();

    private IHandler? _handler;
    private BufferManager? _bufferManager;
    internal static FrameSerializationContext Get(IHandler handler, BufferManager bufferManager)
    {
        var obj = Pool<FrameSerializationContext>.Get();
        obj._handler = handler;
        obj._bufferManager = bufferManager;
        return obj;
    }

    private ScratchBuffer _current;
    private int _headerOffset, _writeOffset, _remaining;

    void WriteFrameHeader(PayloadFlags flags)
    {
        var length = _writeOffset - (_headerOffset + FrameHeader.Size);
        WriteFrameHeader(flags, (ushort)length, ref _current[_headerOffset]);
    }
    void WriteFrameHeader(PayloadFlags flags, ushort length, ref byte destination)
        =>  new FrameHeader(FrameKind.Payload, (byte)flags, _handler!.StreamId, _handler.NextSequenceId(), length).UnsafeWrite(ref destination);

    public override void Complete(byte[] payload)
    {
        SetPayloadLength(payload.Length);
        this.Write(payload);
        Complete();
    }


    void IPooled.Recycle()
    {
        while (_buffers.TryDequeue(out var buffer))
        {   // shouldn't be anything here, but if there is: free it
            buffer.Release();
        }
        _current.Commit(0);
        _current = default;
        _headerOffset = _writeOffset = _remaining = 0;
        _handler = null;
        _bufferManager = null;
    }
}
//    private readonly Queue<BufferSegment> _buffers = new();
//    private byte[] _currentBuffer = Utilities.EmptyBuffer;
//    private int _frameHeaderOffset, _writeOffset, _remaining = -1, _nextSize = InitialBufferSize;
//    private long _totalLength;
//    private bool _isTerminated = false;
//    const int InitialBufferSize = 1024, MaxBufferSize = 64 * 1024;

//    public long Length => _totalLength;

//    private IHandler? _handler;

//    void WriteFrameHeader(PayloadFlags flags, ushort length)
//        => new FrameHeader(_handler!.Kind, (byte)flags, _handler.Id, _handler.NextSequenceId(), length).UnsafeWrite(ref _currentBuffer[_frameHeaderOffset]);

//    public async ValueTask<int> WritePayloadAsync(IFrameConnection connection, bool isLastElement, CancellationToken cancellationToken)
//    {
//        if (!_isTerminated || isLastElement)
//        {
//            GetSpan(0);
//            WriteFrameHeader(
//                flags: isLastElement ? PayloadFlags.NoPayload | PayloadFlags.EndItem | PayloadFlags.EndAllItems : PayloadFlags.NoPayload | PayloadFlags.EndItem,
//                length: 0);
//        }
//        var finalFlags = isLastElement ? (PayloadFlags.EndItem | PayloadFlags.EndAllItems) : PayloadFlags.EndItem;
//        var writes = 0;
//        if (_buffers.TryDequeue(out var frames))
//        {
//            do
//            {
//                await connection.WriteAsync(frames, cancellationToken);
//                writes++;
//            }
//            while (_buffers.TryDequeue(out frames));
//        }

//        if (!_isTerminated)
//        {
//            // write an empty final payload
//            await writer.WriteAsync(new Frame(FrameKind.Payload, handler.Id, (byte)finalFlags), cancellationToken);
//            frames++;
//        }
//        return writes;
//    }
//    public void Recycle()
//    {
//        _handler = null;
//        _buffers.Clear();
//        _totalLength = _frameHeaderOffset = _writeOffset = 0;
//        _remaining = -1;
//        _nextSize = InitialBufferSize;
//        _isTerminated = 0;
//        if (_currentBuffer.Length != 0)
//            ArrayPool<byte>.Shared.Return(_currentBuffer);
//        _currentBuffer = Utilities.EmptyBuffer;
//        Pool<FrameSerializationContext>.Put(this);
//    }

//    internal static FrameSerializationContext Get(IHandler handler)
//    {
//        var obj = Pool<FrameSerializationContext>.Get();
//        obj._handler = handler;
//        obj.GetSpan(0);
//        return obj;
//    }

//    public override IBufferWriter<byte> GetBufferWriter() => this;

//    public override void Complete(byte[] payload)
//    {
//        _totalLength += payload.Length;
//        _buffers.Enqueue((payload, 0, payload.Length, false));
//    }

//    public override void Complete() => Flush(false);

//    //private int Available => _active.Length - _activeCommitted;
//    private void Flush(bool getNew)
//    {
//        var written = _offset - Frame.HeaderBytes;
//        if (written > 0)
//        {
//            _buffers.Enqueue((_currentBuffer, Frame.HeaderBytes, written, true));
//            _totalLength += written;
//        }
//        if (getNew)
//        {
//            var size = _nextSize;
//            if (size < MaxBufferSize) _nextSize <<= 1; // use incrementally bigger buffers, up to a limit
//            _currentBuffer = ArrayPool<byte>.Shared.Rent(size);
//            _offset = Frame.HeaderBytes;
//            _remaining = _currentBuffer.Length - Frame.HeaderBytes;
//        }
//        else
//        {
//            _currentBuffer = Utilities.EmptyBuffer;
//            _remaining = _offset = 0;
//        }
//    }

//    void IBufferWriter<byte>.Advance(int count)
//    {
//        if (count < 0 || count > _remaining) Throw(count, _remaining);
//        _offset += count;
//        _remaining -= count;

//        static void Throw(int count, int _remaining) => throw new ArgumentOutOfRangeException(nameof(count),
//            $"Advance must be called with count in the range [0, {_remaining}], but {count} was specified");
//    }

//    Memory<byte> IBufferWriter<byte>.GetMemory(int sizeHint)
//    {
//        if (Math.Max(sizeHint, 64) > _remaining) Flush(true);
//        return new Memory<byte>(_currentBuffer, _offset, _remaining);
//    }

//    public Span<byte> GetSpan(int sizeHint)
//    {
//        if (Math.Max(sizeHint, 64) > _remaining) Flush(true);
//        return new Span<byte>(_currentBuffer, _offset, _remaining);
//    }
//}
