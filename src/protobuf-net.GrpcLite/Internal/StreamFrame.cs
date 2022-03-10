using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

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

        if (length > buffer.Length)
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
        return new StreamFrame(kind, id, kindFlags, buffer, 0, length, FrameFlags.RecycleBuffer);

        static void ThrowEOF() => throw new EndOfStreamException();
    }
}
internal enum FrameKind : byte
{
    Unknown, // prevent silly errors with zeros
    NewUnary,
    Payload,
    Cancel,
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