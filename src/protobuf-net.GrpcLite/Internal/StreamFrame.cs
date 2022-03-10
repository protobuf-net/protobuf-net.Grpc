using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal;

internal readonly struct StreamFrame : IDisposable
{
    public const int HeaderBytes = 6; // kind=1,kindFlags=1,id=2,length=2

    // order here is to help packing
    public byte[] Buffer { get; }

    public int Offset { get; }
    public ushort Id { get; }
    public ushort Length { get; }

    public FrameKind Kind { get; }
    public FrameFlags FrameFlags { get; }
    public byte KindFlags { get; } // kind-specific

    public StreamFrame(FrameKind kind, ushort id, byte kindFlags) : this(kind, id, kindFlags, Array.Empty<byte>(), 0, 0, FrameFlags.None) { }
    public StreamFrame(FrameKind kind, ushort id, byte kindFlags, byte[] buffer, int offset, ushort length, FrameFlags frameFlags)
    {
        Kind = kind;
        Id = id;
        KindFlags = kindFlags;
        Buffer = buffer;
        Offset = offset;
        Length = length;
        FrameFlags = frameFlags;
    }

    public void Write(byte[] buffer, int offset)
    {
        buffer[offset++] = (byte)Kind;
        buffer[offset++] = KindFlags;
        // note id and length are little-endian
        buffer[offset++] = (byte)(Id & 0xFF);
        buffer[offset++] = (byte)((Id >> 8) & 0xFF);
        buffer[offset++] = (byte)(Length & 0xFF);
        buffer[offset++] = (byte)((Length >> 8) & 0xFF);
    }

    public void Dispose()
    {
        if ((FrameFlags & FrameFlags.RecycleBuffer) != 0) ArrayPool<byte>.Shared.Return(Buffer);
    }

    public override string ToString() => $"[{Id}, {Kind}] {Length} bytes ({FrameFlags}, {KindFlags})";

    public override bool Equals([NotNullWhen(true)] object? obj) => throw new NotSupportedException();
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
                    logger.LogDebug(frame.Length, static (state, _) => $"wrote {state+HeaderBytes} to stream");

                    frame.Dispose(); // recycles buffer if needed; not worried about try/finally here
                }
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

        int remaining = 6, offset = 0, bytesRead;
        while (remaining > 0 && (bytesRead = await stream.ReadAsync(buffer, offset, remaining, cancellationToken)) > 0)
        {
            remaining -= bytesRead;
            offset += bytesRead;
        }
        if (remaining != 0) ThrowEOF();

        var kind = (FrameKind)buffer[0];
        var kindFlags = buffer[1];
        var id = (ushort)(buffer[2] | (buffer[3] << 8));
        var length = (ushort)(buffer[4] | (buffer[5] << 8));

        if (length == 0)
        {   // release the buffer immediately
            ArrayPool<byte>.Shared.Return(buffer);
            buffer = Array.Empty<byte>();
        }
        else if (length > buffer.Length)
        {   // up-size
            ArrayPool<byte>.Shared.Return(buffer);
            buffer = ArrayPool<byte>.Shared.Rent(length);
        }

        remaining = length;
        offset = 0;
        while (remaining > 0 && (bytesRead = await stream.ReadAsync(buffer, offset, remaining, cancellationToken)) > 0)
        {
            remaining -= bytesRead;
            offset += bytesRead;
        }
        if (remaining != 0) ThrowEOF();
        return new StreamFrame(kind, id, kindFlags, buffer, 0, length, length == 0 ? FrameFlags.None : FrameFlags.RecycleBuffer);

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