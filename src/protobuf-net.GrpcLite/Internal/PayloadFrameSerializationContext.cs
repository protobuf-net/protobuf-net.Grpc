using Grpc.Core;
using ProtoBuf.Grpc.Lite.Connections;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite.Internal;

internal sealed class PayloadFrameSerializationContext : SerializationContext, IBufferWriter<byte>, IPooled
{
    private readonly List<Frame> _frames = new();

    private IStream? _stream;
    private FrameHeader _template;
    private int _declaredPayloadLength, _totalLength;
    private Frame.Builder _builder;

    internal int PendingFrameCount => _frames.Count;

    public override string ToString()
        => $"{_totalLength} bytes, {_frames.Count} frames (total bytes: {_totalLength + _frames.Count * FrameHeader.Size})";

    internal static PayloadFrameSerializationContext Get(ushort streamId, RefCountedMemoryPool<byte> pool, FrameKind kind)
    {
        var obj = Pool<PayloadFrameSerializationContext>.Get();
        obj._stream = null;
        obj._template = new FrameHeader(kind, 0, streamId, 0, payloadLength: 0, isFinal: false);
        obj._declaredPayloadLength = -1;
        obj._totalLength = 0;
        obj._builder = Frame.CreateBuilder(pool);
        return obj;
    }
    internal static PayloadFrameSerializationContext Get(IStream stream, RefCountedMemoryPool<byte> pool, FrameKind kind)
    {
        var obj = Pool<PayloadFrameSerializationContext>.Get();
        obj._stream = stream;
        obj._template = new FrameHeader(kind, 0, stream.Id, 0, payloadLength: 0, isFinal: false);
        obj._declaredPayloadLength = -1;
        obj._totalLength = 0;
        obj._builder = Frame.CreateBuilder(pool);
        return obj;
    }

    public override void Complete(byte[] payload)
    {
        SetPayloadLength(payload.Length);
        this.Write(payload);
        Complete();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override IBufferWriter<byte> GetBufferWriter() => this;

    public override void SetPayloadLength(int payloadLength) => _declaredPayloadLength = payloadLength;

    public override void Complete()
    {
        Logging.DebugWriteLine($"[serialize] complete; {_totalLength} committed (declared: {_declaredPayloadLength})");
        if (_declaredPayloadLength >= 0 && _declaredPayloadLength != _totalLength) ThrowLengthMismatch(_declaredPayloadLength, _totalLength);

        // make sure we have a buffer (if we have an existing one, the 0 ensures we don't flush and get fresh
        GetMemoryImpl(0, externalCaller: false);
        Flush(true);

        static void ThrowLengthMismatch(int declared, int actual) => throw new InvalidOperationException(
            $"The length declared via {nameof(SetPayloadLength)} ({declared}) does not match the actual length written ({actual})");
    }

    internal Frame[] DetachPendingFrames()
    {
        var result = _frames.ToArray();
        _frames.Clear(); // we're transferring ownership out, to avoid double-dispose
        return result;
    }

    public void Recycle()
    {
        foreach (var frame in _frames)
        {
            // shouldn't be anything here, but if there is: free it
            frame.Release();
        }
        _frames.Clear();
        _stream = null;
        _builder.Release();
        _template = default;
        Pool<PayloadFrameSerializationContext>.Put(this);
    }

    void IBufferWriter<byte>.Advance(int count)
    {
        if (count != 0) // some serializers call Advance(0) without ever calling GetMemory()/GetSpan(); ask me how I know
        {
            _totalLength += count;
            Logging.DebugWriteLine($"[serialize] committed {count} for a total of {_totalLength}");
            _builder.Advance(count);
        }
    }

    public Memory<byte> GetMemory(int sizeHint)
    {
        var buffer = GetMemoryImpl(sizeHint, externalCaller: true);
        Logging.DebugWriteLine($"[serialize] requested {sizeHint}, provided {buffer.Length}");
        return buffer;
    }
    private Memory<byte> GetMemoryImpl(int sizeHint, bool externalCaller)
    {
        if (externalCaller)
        {
            // the IBufferWriter API is a bit... woolly; we need to fudge things a bit, because: often
            // they'll ask for something humble (or even -ve/zero), and hope for more; we need to facilitate that
            const int REASONABLE_MIN_LENGTH = 128;
            sizeHint = Math.Min(Math.Max(sizeHint, REASONABLE_MIN_LENGTH), FrameHeader.MaxPayloadLength);
        }

        if (_builder.InProgress)
        {
            // continue using the existing buffer if there's something useful there
            var buffer = _builder.GetBuffer();
            if (buffer.Length >= sizeHint) return buffer;

            // otherwise, we'll flush the existing, and use a new frame
            Flush(false);
        }

        Debug.Assert(!_builder.InProgress, "not expecting an in-progress state");
        if (_stream is not null)
        {
            return _builder.NewFrame(_template, _stream!.NextSequenceId(), sizeHint: (ushort)sizeHint);
        }
        else
        {
            var result = _builder.NewFrame(_template, _template.SequenceId, sizeHint: (ushort)sizeHint);
            _template = _template.WithNextSequenceId();
            return result;
        }
    }

    void Flush(bool isFinal)
    {
        if (_builder.InProgress)
        {
            _frames.Add(_builder.CreateFrame(isFinal));
            Debug.Assert(!_builder.InProgress, "not expecting an in-progress state");
        }
        if (isFinal) _builder.Release(); // not expecting any more
    }

    Span<byte> IBufferWriter<byte>.GetSpan(int sizeHint) => GetMemory(sizeHint).Span;

    internal ValueTask WritePayloadAsync(ChannelWriter<(Frame Frame, FrameWriteFlags Flags)> output, FrameWriteFlags flags, CancellationToken cancellationToken)
    {
        switch (_frames.Count)
        {
            case 0:
                return default;
            case 1:
                var frame = _frames[0];
                _frames.Clear();
                var val = (frame, flags);
                return output.TryWrite(val) ? default : output.WriteAsync(val, cancellationToken);
            default:
                return WriteAllTrySync(_frames, output, flags, cancellationToken);
        }

        static ValueTask WriteAllTrySync(List<Frame> frames, ChannelWriter<(Frame Frame, FrameWriteFlags Flags)> output, FrameWriteFlags flags, CancellationToken cancellationToken)
        {
            var iter = frames.GetEnumerator();
            while (iter.MoveNext())
            {
                if (!output.TryWrite((iter.Current, flags)))
                    return WriteAllAsync(frames, iter, output, flags, cancellationToken);
            }
            frames.Clear();
            iter.Dispose(); // no-op, but: let's follow the rules!
            return default; // woohoo, we wrote them all synchronously; no state machines for us!
        }
        static async ValueTask WriteAllAsync(List<Frame> frames, List<Frame>.Enumerator iter, ChannelWriter<(Frame Frame, FrameWriteFlags Flags)> output, FrameWriteFlags flags, CancellationToken cancellationToken)
        {
            do
            {   // we've already had to go sync; let's not try to be clever - just WriteAsync
                await output.WriteAsync((iter.Current, flags), cancellationToken);
            } while (iter.MoveNext());
            frames.Clear();
            iter.Dispose(); // no-op, but: let's follow the rules!
        }
    }
}
