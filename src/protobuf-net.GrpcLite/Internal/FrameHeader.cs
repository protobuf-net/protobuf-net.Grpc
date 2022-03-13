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
                    dest[6] = (byte)(Length & 0xFF);
                    dest[7] = (byte)((Length >> 8) & 0xFF);
                }
            }
        }
    }

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
    private readonly int _offset, _length;
    public BufferSegment(byte[] array, int offset, int length)
    {
        _source = array;
        _offset = offset;
        _length = length;
    }
    internal BufferSegment(Slab slab, int offset, int length)
    {
        _source = slab;
        _offset = offset;
        _length = length;
    }

    public readonly object _source;

    public int Length => _length;

    public ref byte this[int index]
    {
        get
        {
            if (index < 0 || index >= _length) Throw();
            return ref _source is Slab slab ? ref slab[_offset + index] : ref ((byte[])_source)[_offset + index];

            static void Throw() => throw new IndexOutOfRangeException();
        }
    }

    public bool TryGetArray(out ArraySegment<byte> buffer)
    {
        if (_source is Slab slab) return slab.TryGetArray(_offset, _length, out buffer);
        if (_length == 0) return Utilities.TryGetEmptySegment(out buffer);
        buffer = new ArraySegment<byte>((byte[])_source, _offset, _length);
        return true;
    }

    public Memory<byte> Memory
    {
        get
        {
            if (_length == 0) return default;
            if (_source is Slab slab) return slab.Memory.Slice(_offset, _length);
            return new Memory<byte>((byte[])_source, _offset, _length);
        }
    }

    public void Release()
    {
        if (_source is Slab slab) slab.RemoveReference();
    }
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

    public void Deconstruct(out FrameHeader header, out BufferSegment payload)
    {
        header = Header;
        payload = Payload;
    }

}