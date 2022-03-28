using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProtoBuf.Grpc.Lite.Connections;

/// <summary>
/// Represents an encoded gRPC frame header.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = Size)]
public readonly struct FrameHeader : IEquatable<FrameHeader>
{
    /// <summary>
    /// The size required for a <see cref="FrameHeader"/>.
    /// </summary>
    public const int Size = sizeof(ulong); // kind=1,kindFlags=1,reqid=2,seqid=2,length=2

    private const int PayloadLengthOffset = 6;

    /// <summary>
    /// The maximum valid payload length that can be represented in a single <see cref="Frame"/>.
    /// </summary>
    public const ushort MaxPayloadLength = (ushort.MaxValue >> 1) - Size; // so that header+payload fit in 32k
    internal const ushort MSB = 1 << 15;

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
                    dest[1] = (byte)Reserved;
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

    /// <summary>
    /// Create a new <see cref="FrameHeader"/> value.
    /// </summary>
    public FrameHeader(FrameKind kind, byte reserved, ushort streamId, ushort sequenceId, ushort payloadLength = 0, bool isFinal = true)
    {
#if NET5_0_OR_GREATER
        Unsafe.SkipInit(out this);
#else
        RawValue = 0;
#endif
        Kind = kind;
        Reserved = reserved;
        StreamId = streamId;
        SequenceId = sequenceId;
        if (payloadLength > MaxPayloadLength) Throw(); // also enforces MSB not set
        _payloadLengthAndFinal = isFinal ? (ushort)(payloadLength | MSB) : payloadLength;

        static void Throw() => throw new ArgumentOutOfRangeException(nameof(payloadLength));
    }

    /// <summary>
    /// The type of frame being represented.
    /// </summary>

    [field: FieldOffset(0)]
    public FrameKind Kind { get; }

    /// <summary>
    /// Indicates whether this <see cref="FrameKind"/> is valid, i.e. not <see cref="FrameKind.None"/>.
    /// </summary>
    public bool HasValue => Kind != FrameKind.None;

    internal FrameHeader(in FrameHeader template, ushort sequenceId)
    {
        this = template;
        SequenceId = sequenceId;
        _payloadLengthAndFinal = 0;
    }

    /// <summary>
    /// Gets any optional flags associated with this frame.
    /// </summary>
    [field: FieldOffset(1)]
    public byte Reserved { get; }

    /// <summary>
    /// Gets the stream identifier of this value.
    /// </summary>
    [field: FieldOffset(2)]
    public ushort StreamId { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)] // mostly useful for little-endian mode
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
                        reserved: ptr[1],
                        streamId: (ushort)(ptr[2] | (ptr[3] << 8)),
                        sequenceId: (ushort)(ptr[4] | (ptr[5] << 8)),
                        payloadLength: (ushort)(ptr[6] | (ptr[7] << 8))
                    );
                }
            }
        }
    }

    /// <summary>
    /// Gets the sequence identifier of this value.
    /// </summary>

    [field: FieldOffset(4)]
    public ushort SequenceId { get; }
    [field: FieldOffset(6)]
    private readonly ushort _payloadLengthAndFinal;

    internal FrameHeader WithNextSequenceId() => new FrameHeader(in this, (ushort)(SequenceId + 1));

    /// <summary>
    /// Gets the length of the payload data in this frame.
    /// </summary>
    public ushort PayloadLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ushort)(_payloadLengthAndFinal & ~MSB);
    }

    /// <summary>
    /// Gets the length of the payload data that would be represented by the header value provided.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetPayloadLength(ReadOnlySpan<byte> header)
        => (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(PayloadLengthOffset)) & ~MSB);

    internal static void SetPayloadLength(Span<byte> header, ushort payloadLength) // WARNING: wipes "final"
    {
        Frame.AssertValidLength(payloadLength);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(PayloadLengthOffset), payloadLength);
    }

    internal static void SetFinal(Span<byte> header)
        => header[PayloadLengthOffset + 1] |= 0x80; // + 1 because little-endian, so MSB is in second octet

    /// <summary>
    /// Indicates that this is the last frame in a group that collectively make up a <see cref="FrameKind.StreamHeader"/>, <see cref="FrameKind.StreamPayload"/>, <see cref="FrameKind.StreamTrailer"/>, etc.
    /// </summary>
    public bool IsFinal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_payloadLengthAndFinal & MSB) != 0;
    }

    /// <summary>
    /// Indicates if this stream was initiated from a client, vs a server.
    /// </summary>
    public bool IsClientStream
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (StreamId & MSB) == 0;
    }
    

    /// <inheritdoc/>
    public override string ToString() => $"[{StreamId}/{SequenceId}:{Kind}], {PayloadLength} bytes (final: {IsFinal}, rsv: {Reserved})";

    /// <inheritdoc/>
    public override int GetHashCode() => RawValue.GetHashCode();

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is FrameHeader other && RawValue == other.RawValue;

    /// <summary>
    /// Compares this value with another <see cref="FrameHeader"/> value.
    /// </summary>
    public bool Equals(FrameHeader other) => RawValue == other.RawValue;
}