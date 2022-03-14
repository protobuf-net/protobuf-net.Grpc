using Grpc.Core;
using ProtoBuf.Grpc.Lite.Connections;
using System.Buffers;
using System.Diagnostics;

namespace ProtoBuf.Grpc.Lite.Internal;

internal sealed class PayloadFrameSerializationContext : SerializationContext, IBufferWriter<byte>, IPooled
{
    private readonly List<Frame> _frames = new();

    private IHandler? _handler;
    private FrameBufferManager? _bufferManager;
    private FrameHeader _template;
    private int _declaredPayloadLength, _totalLength;
    internal static PayloadFrameSerializationContext Get(IHandler handler, FrameBufferManager bufferManager, ushort streamId, PayloadFlags flags)
    {
        var obj = Pool<PayloadFrameSerializationContext>.Get();
        obj._handler = handler;
        obj._bufferManager = bufferManager;
        flags &= ~PayloadFlags.EndItem; // we'll be the judge of that, thanks
        obj._template = new FrameHeader(FrameKind.Payload, (byte)flags, streamId, 0, 0);
        obj._declaredPayloadLength = -1;
        obj._totalLength = 0;
        return obj;
    }

    private FrameBufferManager.Slab? _current;

    public override void Complete(byte[] payload)
    {
        SetPayloadLength(payload.Length);
        this.Write(payload);
        Complete();
    }

    public override IBufferWriter<byte> GetBufferWriter() => this;

    public override void SetPayloadLength(int payloadLength) => _declaredPayloadLength = payloadLength;

    public override void Complete()
    {
        Debug.WriteLine($"[serialize] complete; {_totalLength} committed (declared: {_declaredPayloadLength})");
        if (_declaredPayloadLength >= 0 && _declaredPayloadLength != _totalLength) ThrowLengthMismatch(_declaredPayloadLength, _totalLength);
        // update our template to add the "we're done" flag
        _template = new FrameHeader(_template, (byte)(_template.KindFlags | (byte)PayloadFlags.EndItem));
        // make sure we have a buffer (if we have an existing one, the 0 ensures we don't flush and get fresh
        GetMemoryImpl(0);
        Flush();

        static void ThrowLengthMismatch(int declared, int actual) => throw new InvalidOperationException(
            $"The length declared via {nameof(SetPayloadLength)} ({declared}) does not match the actual length written ({actual})");
    }


    public void Recycle()
    {
        foreach (var frame in _frames)
        {
            // shouldn't be anything here, but if there is: free it
            frame.Release();
        }
        _frames.Clear();
        _current?.Return();
        _current = null;
        _handler = null;
        _bufferManager = null;
        _template = default;
        Pool<PayloadFrameSerializationContext>.Put(this);
    }

    void IBufferWriter<byte>.Advance(int count)
    {
        _totalLength += count;
        Debug.WriteLine($"[serialize] committed {count} for a total of {_totalLength}; {_current!.DebugSummarize(count)}: {_current!.DebugGetHex(count)}");
        _current!.Advance(count);
    }

    public Memory<byte> GetMemory(int sizeHint)
    {
        var buffer = GetMemoryImpl(Math.Max(sizeHint, 8)); // always give external callers *at least something*
        Debug.WriteLine($"[serialize] requested {sizeHint}, provided {_current!.DebugSummarize(buffer)}");
        return buffer;
    }
    private Memory<byte> GetMemoryImpl(int minBytes)
    {   // note that GetMemoryImpl allows a zero-length request that means "just make sure we have allocated a buffer"
        var current = _current;
        const int REASONABLE_MIN_LENGTH = 128;
        Memory<byte> buffer;
        if (current is not null)
        {
            // continue using the existing buffer if there's something useful there
            buffer = current.ActiveBuffer;
            if (buffer.Length >= Math.Max(minBytes, REASONABLE_MIN_LENGTH)) return buffer;

            // otherwise, give up; we'll create a new buffer
            Flush();
        }
        _current = current = _bufferManager!.Rent(minBytes);

        buffer = current.ActiveBuffer;
        if (buffer.Length < Math.Max(minBytes, REASONABLE_MIN_LENGTH))
        {
            current.Return();
            throw new InvalidOperationException($"Newnly rented slab is undersized at {buffer.Length} bytes");
        }
        Debug.WriteLine($"[serialize] rented slab; header is [{_current.CurrentHeaderOffset}, {_current.CurrentHeaderOffset + FrameHeader.Size})");
        return buffer;
    }

    void Flush()
    {
        var current = _current;
        _current = null;
        if (current is not null)
        {
            var header = new FrameHeader(_template, _handler!.NextSequenceId());
            var buffer = current.CreateFrameAndInvalidate(header, updateHeaderLength: true);
            _frames.Add(buffer);
            current.Return();
            // if we're flushing, it is because *either* we don't have enought space left for additional data,
            // *or* we're all done; either way: we're done with the existing buffer!
        }
    }

    Span<byte> IBufferWriter<byte>.GetSpan(int sizeHint) => GetMemory(sizeHint).Span;

    internal ValueTask WritePayloadAsync(IFrameConnection output, CancellationToken cancellationToken)
    {
        switch (_frames.Count)
        {
            case 0:
                return default;
            case 1:
                var frame = _frames[0];
                _frames.Clear();
                return output.WriteAsync(frame, cancellationToken);
            default:
                // TODO: use List<T> and cheat
                return WriteAll(_frames, output, cancellationToken);
        }

        static async ValueTask WriteAll(List<Frame> frames, IFrameConnection output, CancellationToken cancellationToken)
        {
            try
            {
                foreach (var frame in frames)
                {
                    await output.WriteAsync(frame, cancellationToken);
                }
            }
            finally
            {
                frames.Clear();
            }
        }
    }
}
