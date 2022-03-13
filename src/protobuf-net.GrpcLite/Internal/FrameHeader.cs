using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProtoBuf.Grpc.Lite.Internal;

public enum FrameKind : byte
{
    // note that as a convenience, these match Grpc.Core.MethodType
    // for both name and value
    Unary,
    ClientStreaming,
    ServerStreaming,
    DuplexStreaming,

    Payload,
    Cancel,
    Close,
    Ping,
    [Obsolete("remove this later; should be a structured response status")]
    MethodNotFound,
}

[StructLayout(LayoutKind.Explicit, Size = Size)]
public readonly struct FrameHeader : IEquatable<FrameHeader>
{
    public const int Size = sizeof(ulong); // kind=1,kindFlags=1,reqid=2,seqid=2,length=2

    // this overlaps everything and can be considered "the value", in CPU-endianness
    [FieldOffset(0)]
    private readonly ulong RawValue;

    public FrameHeader(FrameKind kind, byte kindFlags, ushort streamId, ushort sequenceId, ushort length)
    {
#if NET5_0_OR_GREATER
        Unsafe.SkipInit(out this);
#else
        RawValue = 0;
#endif
        Kind = kind;
        KindFlags = kindFlags;
        StreamId = streamId;
        SequenceId = sequenceId;
        Length = length;
    }

    [field:FieldOffset(0)]
    public FrameKind Kind { get; }
    [field: FieldOffset(1)]
    public byte KindFlags { get; } // kind-specific
    [field: FieldOffset(2)]
    public ushort StreamId { get; }

    internal static FrameHeader ReadUnsafe(ref byte source)
    {
        if (BitConverter.IsLittleEndian)
        {
            return Unsafe.As<byte, FrameHeader>(ref source);
        }
        else
        {
            unsafe
            {
                fixed (byte* ptr = &source)
                {
                    return new FrameHeader(
                        kind: (FrameKind)ptr[0],
                        kindFlags: ptr[1],
                        streamId: (ushort)(ptr[2] | (ptr[3] << 8)),
                        sequenceId: (ushort)(ptr[4] | (ptr[5] << 8)),
                        length: (ushort)(ptr[6] | (ptr[7] << 8))
                    );
                }
            }
        }
    }

    [field: FieldOffset(4)]
    public ushort SequenceId { get; }
    [field: FieldOffset(6)]
    public ushort Length { get; }

    private string FormattedFlags => Kind switch
    {
        FrameKind.Payload => ((PayloadFlags)KindFlags).ToString(),
        FrameKind.Close => ((GeneralFlags)KindFlags).ToString(),
        FrameKind.Ping => ((GeneralFlags)KindFlags).ToString(),
        _ => KindFlags.ToString(),
    };

    /// <inheritdoc>
    public override string ToString() => $"[{StreamId}/{SequenceId}:{Kind}],{Length},{FormattedFlags}";

    /// <inheritdoc>
    public override int GetHashCode() => RawValue.GetHashCode();

    /// <inheritdoc>
    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FrameHeader other && RawValue == other.RawValue;

    public bool Equals(FrameHeader other) => RawValue == other.RawValue;
}

public readonly struct BufferSegment
{
    public static BufferSegment Empty = new BufferSegment(Utilities.EmptyBuffer, 0, 0);
    public BufferSegment(byte[] array, int offset, int length)
    {
        Array = array;
        Offset = offset;
        Length = length;
    }
    public int Offset { get; }
    public int Length { get; }
    public byte[] Array { get; }
    public void Release() { }

    public static implicit operator ArraySegment<byte>(in BufferSegment segment)
        => new ArraySegment<byte>(segment.Array, segment.Offset, segment.Length);
    public static  implicit operator Span<byte>(in BufferSegment segment)
        => new Span<byte>(segment.Array, segment.Offset, segment.Length);
    public static implicit operator ReadOnlySpan<byte>(in BufferSegment segment)
        => new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Length);
    public static implicit operator Memory<byte>(in BufferSegment segment)
        => new Memory<byte>(segment.Array, segment.Offset, segment.Length);
    public static implicit operator ReadOnlyMemory<byte>(in BufferSegment segment)
        => new ReadOnlyMemory<byte>(segment.Array, segment.Offset, segment.Length);
    public static implicit operator ReadOnlySequence<byte>(in BufferSegment segment)
        => new ReadOnlySequence<byte>(segment.Array, segment.Offset, segment.Length);
} 
public readonly struct NewFrame
{
    public NewFrame(FrameHeader header) : this()
    {
        Header = header;
        Payload = BufferSegment.Empty;
    }
    public NewFrame(FrameHeader header, BufferSegment payload)
    {
        if (header.Length != payload.Length) ThrowLengthMismatch();
        Header = header;
        Payload = payload;
        static void ThrowLengthMismatch() => throw new ArgumentException("The length of the header and payload must agree", nameof(payload));
    }

    public FrameHeader Header { get; }
    public BufferSegment Payload { get; }
}