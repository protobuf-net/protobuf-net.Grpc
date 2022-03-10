using System.Diagnostics.CodeAnalysis;

namespace ProtoBuf.Grpc.Lite.Internal;

internal readonly struct StreamFrame
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

    public override string ToString() => $"[{Id}, {Kind}] {Length} bytes ({FrameFlags}, {KindFlags})";

    public override bool Equals([NotNullWhen(true)] object? obj) => throw new NotSupportedException();
    public override int GetHashCode() => throw new NotSupportedException();
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