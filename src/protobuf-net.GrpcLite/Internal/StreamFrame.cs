using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal;

[StructLayout(LayoutKind.Explicit)]
internal readonly struct StreamFrame : IDisposable
{
    public const int HeaderBytes = 8; // kind=1,kindFlags=1,reqid=2,seqid=2,length=2

    [FieldOffset(0)]
    public readonly FrameKind Kind;
    [FieldOffset(1)]
    public readonly byte KindFlags; // kind-specific
    [FieldOffset(2)]
    public readonly ushort RequestId;
    [FieldOffset(4)]
    public readonly ushort SequenceId;
    [FieldOffset(6)]
    public readonly ushort Length;

    // everything below here is not part of the binary payload
    [FieldOffset(8)]
    public readonly int Offset;
    [FieldOffset(12)]
    public readonly FrameFlags FrameFlags;

    [FieldOffset(16)]
    public readonly byte[] Buffer;

    public StreamFrame(FrameKind kind, ushort id, byte kindFlags) : this(kind, id, kindFlags, Array.Empty<byte>(), 0, 0, FrameFlags.None, 0) { }
    public StreamFrame(FrameKind kind, ushort id, byte kindFlags, byte[] buffer, int offset, ushort length, FrameFlags frameFlags, ushort sequenceId = 0)
    {
        Kind = kind;
        RequestId = id;
        KindFlags = kindFlags;
        Buffer = buffer;
        Offset = offset;
        Length = length;
        FrameFlags = frameFlags;
        SequenceId = sequenceId;
    }

    public unsafe void Write(byte[] buffer, int offset)
    {
        // note values are little-endian; the JIT will remove the appropriate dead branch here
        if (BitConverter.IsLittleEndian)
        {
            fixed (void* dest = &buffer[offset])
            fixed (void* src = &Kind)
            {
                if (HeaderBytes == sizeof(ulong))
                {
                    *(ulong*)dest = *(ulong*)src;
                }
                else
                {
                    // unreachable; this will be removed by the compiler, and exists mostly
                    // so that if we change the defintion, it'll a: keep working, and
                    // b: raise a non-suppressed CS0162 on the line above, so we fix it!
#pragma warning disable CS0162
                    Unsafe.CopyBlockUnaligned(dest, src, HeaderBytes);
#pragma warning restore CS0162
                }
            }
        }
        else
        {
            fixed (byte* dest = &buffer[offset])
            {
                dest[0] = (byte)Kind;
                dest[1] = KindFlags;
                dest[2] = (byte)(RequestId & 0xFF);
                dest[3] = (byte)((RequestId >> 8) & 0xFF);
                dest[4] = (byte)(SequenceId & 0xFF);
                dest[5] = (byte)((SequenceId >> 8) & 0xFF);
                dest[6] = (byte)(Length & 0xFF);
                dest[7] = (byte)((Length >> 8) & 0xFF);
            }
        }
    }
    private unsafe StreamFrame(byte[] buffer, int offset)
    {
#if NET5_0_OR_GREATER
        Unsafe.SkipInit(out this);
        Offset = 0;
        FrameFlags = FrameFlags.None;
#else
        this = default;
#endif
        Buffer = Array.Empty<byte>();

        // note values are little-endian; the JIT will remove the appropriate dead branch here
        if (BitConverter.IsLittleEndian)
        {
            fixed (void* src = &buffer[offset])
            fixed (void* dest = &Kind)
            {
                if (HeaderBytes == sizeof(ulong))
                {
                    *(ulong*)dest = *(ulong*)src;
                }
                else
                {
                    // unreachable; this will be removed by the compiler, and exists mostly
                    // so that if we change the defintion, it'll a: keep working, and
                    // b: raise a non-suppressed CS0162 on the line above, so we fix it!
#pragma warning disable CS0162
                    Unsafe.CopyBlockUnaligned(dest, src, HeaderBytes);
#pragma warning restore CS0162
                }
            }
        }
        else
        {
            fixed (byte* ptr = &buffer[offset])
            {
                Kind = (FrameKind)ptr[0];
                KindFlags = ptr[1];
                RequestId = (ushort)(ptr[2] | (ptr[3] << 8));
                SequenceId = (ushort)(ptr[4] | (ptr[5] << 8));
                Length = (ushort)(ptr[6] | (ptr[7] << 8));
            }
        }
    }

    private StreamFrame(in StreamFrame from, byte[] buffer, int offset, FrameFlags frameFlags)
    {
        this = from;
        Buffer = buffer;
        Offset = offset;
        FrameFlags = frameFlags;
    }

    public void Dispose()
    {
        if ((FrameFlags & FrameFlags.RecycleBuffer) != 0) ArrayPool<byte>.Shared.Return(Buffer);
    }

    public override string ToString() => $"[{RequestId}, {Kind}] {Length} bytes ({FrameFlags}, {KindFlags})";

    public override bool Equals(object? obj) => throw new NotSupportedException();
    public override int GetHashCode() => throw new NotSupportedException();

    public static StreamFrame GetInitializeFrame(FrameKind kind, ushort id, string fullName, string? host)
    {
        if (string.IsNullOrEmpty(fullName)) ThrowMissingMethod();
        if (!string.IsNullOrEmpty(host)) ThrowNotSupported(); // in future: delimit?
        var length = Encoding.UTF8.GetByteCount(fullName);
        if (length > ushort.MaxValue) ThrowMethodTooLarge(length);

        var buffer = ArrayPool<byte>.Shared.Rent(StreamFrame.HeaderBytes + length);
        var actualLength = Encoding.UTF8.GetBytes(fullName, 0, fullName.Length, buffer, StreamFrame.HeaderBytes);
        Debug.Assert(actualLength == length, "length mismatch in encoding!");

        return new StreamFrame(kind, id, 0, buffer, StreamFrame.HeaderBytes, (ushort)length, FrameFlags.RecycleBuffer | FrameFlags.HeaderReserved);

        static void ThrowMissingMethod() => throw new ArgumentOutOfRangeException(nameof(fullName), "No method name was specified");
        static void ThrowNotSupported() => throw new ArgumentOutOfRangeException(nameof(host), "Non-empty hosts are not currently supported");
        static void ThrowMethodTooLarge(int length) => throw new InvalidOperationException($"The method name is too large at {length} bytes");
    }

    private static readonly UnboundedChannelOptions OutboundOptions = new()
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = true,
    };
    internal static Channel<StreamFrame> CreateChannel()
        => Channel.CreateUnbounded<StreamFrame>(OutboundOptions);

    internal async static Task WriteFromOutboundChannelToStream(Channel<StreamFrame> source, Stream output, ILogger? logger, CancellationToken cancellationToken)
    {
        await Task.Yield(); // ensure we don't block the constructor
        byte[]? headerBuffer = null;
        try
        {
            while (await source.Reader.WaitToReadAsync(cancellationToken))
            {
                while (source.Reader.TryRead(out var frame))
                {
                    logger.LogDebug(frame, static (state, _) => $"received {state}");
                    var frameFlags = frame.FrameFlags;
                    if ((frameFlags & FrameFlags.HeaderReserved) != 0)
                    {
                        // we can write the header into the existing buffer, and use a single write
                        var offset = frame.Offset - StreamFrame.HeaderBytes;
                        frame.Write(frame.Buffer, offset);
                        await output.WriteAsync(frame.Buffer, offset, frame.Length + StreamFrame.HeaderBytes, cancellationToken);
                    }
                    else
                    {
                        // use a scratch-buffer for the header, and write the header and payload separately
                        frame.Write(headerBuffer ??= ArrayPool<byte>.Shared.Rent(StreamFrame.HeaderBytes), 0);
                        await output.WriteAsync(headerBuffer, 0, StreamFrame.HeaderBytes, cancellationToken);
                        if (frame.Length != 0)
                        {
                            await output.WriteAsync(frame.Buffer, frame.Offset, frame.Length, cancellationToken);
                        }
                    }
                    logger.LogDebug(frame.Length, static (state, _) => $"wrote {state + HeaderBytes} to stream");

                    frame.Dispose(); // recycles buffer if needed; not worried about try/finally here
                }
                // since we've hit a pause in the available data: flush
                await output.FlushAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // block the writer, since we're doomed
            source.Writer.TryComplete(ex);
        }
        finally
        {
            if (headerBuffer is not null) ArrayPool<byte>.Shared.Return(headerBuffer);
        }
    }

    public static async ValueTask<StreamFrame> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(256);

        int remaining = StreamFrame.HeaderBytes, offset = 0, bytesRead;
        while (remaining > 0 && (bytesRead = await stream.ReadAsync(buffer, offset, remaining, cancellationToken)) > 0)
        {
            remaining -= bytesRead;
            offset += bytesRead;
        }
        if (remaining != 0) ThrowEOF();

        var frame = new StreamFrame(buffer, 0);

        if (frame.Length == 0)
        {   // release the buffer immediately
            ArrayPool<byte>.Shared.Return(buffer);
            return frame; // we're done
        }
        else if (frame.Length > buffer.Length)
        {   // up-size
            ArrayPool<byte>.Shared.Return(buffer);
            buffer = ArrayPool<byte>.Shared.Rent(frame.Length);
        }

        remaining = frame.Length;
        offset = 0;
        while (remaining > 0 && (bytesRead = await stream.ReadAsync(buffer, offset, remaining, cancellationToken)) > 0)
        {
            remaining -= bytesRead;
            offset += bytesRead;
        }
        if (remaining != 0) ThrowEOF();
        return new StreamFrame(frame, buffer, 0, FrameFlags.RecycleBuffer);

        static void ThrowEOF() => throw new EndOfStreamException();
    }
}
internal enum FrameKind : byte
{
    Unknown, // prevent silly errors with zeros
    NewUnary,
    NewClientStreaming,
    NewServerStreaming,
    NewDuplex,
    Payload,
    Cancel,
    Close,
    Ping,
    MethodNotFound,
}
[Flags]
internal enum FrameFlags : byte
{
    None = 0,
    RecycleBuffer = 1 << 0,
    HeaderReserved = 1 << 1,
}

[Flags]
internal enum PayloadFlags
{
    None = 0,
    Final = 1 << 0,
}
[Flags]
internal enum GeneralFlags
{
    None = 0,
    IsResponse = 1 << 0,
}