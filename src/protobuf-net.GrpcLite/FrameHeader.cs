using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProtoBuf.Grpc.Lite;
[StructLayout(LayoutKind.Explicit, Size = Size)]
public readonly struct FrameHeader : IEquatable<FrameHeader>
{
    public const int Size = sizeof(ulong); // kind=1,kindFlags=1,reqid=2,seqid=2,length=2

    internal const int PayloadLengthOffset = 6;
    public const ushort MaxPayloadSize = ushort.MaxValue - Size; // so that header+payload fit in 64k

    internal void UnsafeWrite(ref byte destination)
    {
        // note values are little-endian; the JIT will remove the appropriate dead branch here
        if (BitConverter.IsLittleEndian)
        {
            Unsafe.As<byte, ulong>(ref destination) = RawValue;
        }
        else
        {
            unsafe
            {
                fixed (byte* dest = &destination)
                {
                    dest[0] = (byte)Kind;
                    dest[1] = KindFlags;
                    dest[2] = (byte)(StreamId & 0xFF);
                    dest[3] = (byte)((StreamId >> 8) & 0xFF);
                    dest[4] = (byte)(SequenceId & 0xFF);
                    dest[5] = (byte)((SequenceId >> 8) & 0xFF);
                    dest[6] = (byte)(PayloadLength & 0xFF);
                    dest[7] = (byte)((PayloadLength >> 8) & 0xFF);
                }
            }
        }
    }

    // this overlaps everything and can be considered "the value", in CPU-endianness
    [FieldOffset(0)]
    private readonly ulong RawValue;

    public FrameHeader(FrameKind kind, byte kindFlags, ushort streamId, ushort sequenceId, ushort length = 0)
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
        PayloadLength = length;
    }

    [field: FieldOffset(0)]
    public FrameKind Kind { get; }

    public FrameHeader(in FrameHeader template, ushort sequenceId)
    {
        this = template;
        SequenceId = sequenceId;
    }

    public FrameHeader(in FrameHeader template, byte kindFlags)
    {
        this = template;
        KindFlags = kindFlags;
    }

    [field: FieldOffset(1)]
    public byte KindFlags { get; } // kind-specific
    [field: FieldOffset(2)]
    public ushort StreamId { get; }

    internal static FrameHeader ReadUnsafe(in byte source)
    {
        if (BitConverter.IsLittleEndian)
        {
            return Unsafe.As<byte, FrameHeader>(ref Unsafe.AsRef(in source));
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
    public ushort PayloadLength { get; }

    private string FormattedFlags => Kind switch
    {
        FrameKind.Payload => ((PayloadFlags)KindFlags).ToString(),
        FrameKind.Close => ((GeneralFlags)KindFlags).ToString(),
        FrameKind.Ping => ((GeneralFlags)KindFlags).ToString(),
        _ => KindFlags.ToString(),
    };

    /// <inheritdoc>
    public override string ToString() => $"[{StreamId}/{SequenceId}:{Kind}],{PayloadLength},{FormattedFlags}";

    /// <inheritdoc>
    public override int GetHashCode() => RawValue.GetHashCode();

    /// <inheritdoc>
    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FrameHeader other && RawValue == other.RawValue;

    public bool Equals(FrameHeader other) => RawValue == other.RawValue;
}