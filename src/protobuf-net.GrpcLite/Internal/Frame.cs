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
internal readonly struct Frame : IDisposable
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

    public Frame(FrameKind kind, ushort id, byte kindFlags) : this(kind, id, kindFlags, Utilities.EmptyBuffer, 0, 0, FrameFlags.None, 0) { }
    public Frame(FrameKind kind, ushort id, byte kindFlags, byte[] buffer, int offset, ushort length, FrameFlags frameFlags, ushort sequenceId)
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

    internal void UnsafeWrite(ref byte destination)
    {
        // note values are little-endian; the JIT will remove the appropriate dead branch here
        if (BitConverter.IsLittleEndian)
        {
            // overstomp the header bytes directly ("Kind" is the first field)
            Unsafe.As<byte, ulong>(ref destination) = Unsafe.As<FrameKind, ulong>(ref Unsafe.AsRef(in this.Kind));
        }
        else
        {
            unsafe
            {
                fixed (byte* dest = &destination)
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
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Frame UnsafeRead(ref byte source) => new Frame(ref source);
    private Frame(ref byte source) // unsafe direct initialize
    {
#if NET5_0_OR_GREATER
        Unsafe.SkipInit(out this);
        Offset = 0; // still need to make sure that these get set
        FrameFlags = FrameFlags.None; // to something
#else
        this = default;
#endif
        Buffer = Utilities.EmptyBuffer;

        // note values are little-endian; the JIT will remove the appropriate dead branch here
        if (BitConverter.IsLittleEndian)
        {
            // overstomp the header bytes directly ("Kind" is the first field)
            Unsafe.As<FrameKind, ulong>(ref this.Kind) = Unsafe.As<byte, ulong>(ref source);
        }
        else
        {
            unsafe
            {
                fixed (byte* ptr = &source)
                {
                    Kind = (FrameKind)ptr[0];
                    KindFlags = ptr[1];
                    RequestId = (ushort)(ptr[2] | (ptr[3] << 8));
                    SequenceId = (ushort)(ptr[4] | (ptr[5] << 8));
                    Length = (ushort)(ptr[6] | (ptr[7] << 8));
                }
            }
        }
    }

    internal Frame(in Frame from, byte[] buffer, int offset, FrameFlags frameFlags)
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

    private string FormattedFlags => Kind switch
    {
        FrameKind.Payload => ((PayloadFlags)KindFlags).ToString(),
        FrameKind.Close => ((GeneralFlags)KindFlags).ToString(),
        FrameKind.Ping=> ((GeneralFlags)KindFlags).ToString(),
        _ => KindFlags.ToString(),
    };
    public override string ToString() => $"[{RequestId}/{SequenceId}:{Kind}] {Length} bytes ({FrameFlags}, {FormattedFlags})";

    public override bool Equals(object? obj) => throw new NotSupportedException();
    public override int GetHashCode() => throw new NotSupportedException();

    public static Frame GetInitializeFrame(FrameKind kind, ushort id, ushort sequenceId, string fullName, string? host)
    {
        if (string.IsNullOrEmpty(fullName)) ThrowMissingMethod();
        if (!string.IsNullOrEmpty(host)) ThrowNotSupported(); // in future: delimit?
        var length = Encoding.UTF8.GetByteCount(fullName);
        if (length > ushort.MaxValue) ThrowMethodTooLarge(length);

        var buffer = ArrayPool<byte>.Shared.Rent(Frame.HeaderBytes + length);
        var actualLength = Encoding.UTF8.GetBytes(fullName, 0, fullName.Length, buffer, Frame.HeaderBytes);
        Debug.Assert(actualLength == length, "length mismatch in encoding!");

        return new Frame(kind, id, 0, buffer, Frame.HeaderBytes, (ushort)length, FrameFlags.RecycleBuffer | FrameFlags.HeaderReserved, sequenceId);

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
    internal static Channel<Frame> CreateChannel()
        => Channel.CreateUnbounded<Frame>(OutboundOptions);

    internal async static Task WriteFromOutboundChannelToStream(Channel<Frame> source, Stream output, ILogger? logger, CancellationToken cancellationToken)
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
                        var offset = frame.Offset - Frame.HeaderBytes;
                        frame.UnsafeWrite(ref frame.Buffer[offset]);
                        await output.WriteAsync(frame.Buffer, offset, frame.Length + Frame.HeaderBytes, cancellationToken);
                    }
                    else
                    {
                        // use a scratch-buffer for the header, and write the header and payload separately
                        headerBuffer ??= ArrayPool<byte>.Shared.Rent(Frame.HeaderBytes);
                        frame.UnsafeWrite(ref headerBuffer[0]);
                        await output.WriteAsync(headerBuffer, 0, Frame.HeaderBytes, cancellationToken);
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

    public static async ValueTask<Frame> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(256);

        int remaining = Frame.HeaderBytes, offset = 0, bytesRead;
        while (remaining > 0 && (bytesRead = await stream.ReadAsync(buffer, offset, remaining, cancellationToken)) > 0)
        {
            remaining -= bytesRead;
            offset += bytesRead;
        }
        if (remaining != 0) ThrowEOF();

        var frame = Frame.UnsafeRead(ref buffer[0]);

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
        return new Frame(frame, buffer, 0, FrameFlags.RecycleBuffer);

        static void ThrowEOF() => throw new EndOfStreamException();
    }
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
    EndItem = 1 << 0, // terminates a single streaming object (which could be split over multiple frames)
    EndAllItems = 1 << 1, // terminates a sequence of streaming objects
    NoPayload = 1 << 2, // signals that this object should be discarded; should only be sent as a stream terminator, i.e. EndItem | EndAllItems | NoPayload
}
[Flags]
internal enum GeneralFlags
{
    None = 0,
    IsResponse = 1 << 0,
}