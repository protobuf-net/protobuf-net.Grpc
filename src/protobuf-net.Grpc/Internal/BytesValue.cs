using Grpc.Core;
using ProtoBuf.Meta;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace ProtoBuf.Grpc.Internal;


/// <summary>
/// Represents a single BytesValue chunk (as per <a href="https://github.com/protocolbuffers/protobuf/blob/main/src/google/protobuf/wrappers.proto">wrappers.proto</a>)
/// </summary>
[ProtoContract(Name = ".google.protobuf.BytesValue")]
[Obsolete(Reshape.WarningMessage, false)]
[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
public sealed class BytesValue(byte[] oversized, int length, bool pooled)
{
    /// <summary>
    /// Indicates the maximum length supported for individual chunks when using API rewriting.
    /// </summary>
    public const int MaxLength = 0x1FFFFF; // 21 bits of length prefix; 2,097,151 bytes
                                           // (note we will still *read* buffers larger than that, because of non-"us" endpoints, but we'll never send them)


#if DEBUG
    private static int _fastPassMiss = 0;
    internal static int FastPassMiss => Volatile.Read(ref _fastPassMiss);
#endif

    [Flags]
    enum Flags : byte
    {
        None = 0,
        Pooled = 1 << 0,
        Recycled = 1 << 1,
    }
    private Flags _flags = pooled ? Flags.Pooled : Flags.None;
    private byte[] _oversized = oversized;
    private int _length = length;

    private BytesValue() : this([], 0, false) { } // for deserialization 

    internal bool IsPooled => (_flags & Flags.Pooled) != 0;

    internal bool IsRecycled => (_flags & Flags.Recycled) != 0;

    /// <summary>
    /// Gets or sets the value as a right-sized array
    /// </summary>
    [ProtoMember(1)]
    public byte[] RightSized // for deserializer only
    {
        get
        {
            ThrowIfRecycled();
            if (_oversized.Length != _length)
            {
                Array.Resize(ref _oversized, _length);
                _flags &= ~Flags.Pooled;
            }
            return _oversized;
        }
        set
        {
            value ??= [];
            _length = value.Length;
            _oversized = value;
        }
    }

    /// <summary>
    /// Recycles this instance, releasing the buffer (if pooled), and resetting the length to zero.
    /// </summary>
    public void Recycle()
    {
        var flags = _flags;
        _flags = Flags.Recycled;
        var tmp = _oversized;
        _length = 0;
        _oversized = [];

        if ((flags & Flags.Pooled) != 0)
        {
            ArrayPool<byte>.Shared.Return(tmp);
        }
    }

    private void ThrowIfRecycled()
    {
        if ((_flags & Flags.Recycled) != 0)
        {
            Throw();
        }
        static void Throw() => throw new InvalidOperationException("This " + nameof(BytesValue) + " instance has been recycled");
    }

    /// <summary>
    /// Indicates whether this value is empty (zero bytes)
    /// </summary>
    public bool IsEmpty => _length == 0;

    /// <summary>
    /// Gets the size (in bytes) of this value
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets the payload as an <see cref="ArraySegment{T}"/>
    /// </summary>
    public ArraySegment<byte> ArraySegment
    {
        get
        {
            ThrowIfRecycled();
            return new(_oversized, 0, _length);
        }
    }

    /// <summary>
    /// Gets the payload as a <see cref="ReadOnlySpan{T}"/>
    /// </summary>
    public ReadOnlySpan<byte> Span
    {
        get
        {
            ThrowIfRecycled();
            return new(_oversized, 0, _length);
        }
    }

    /// <summary>
    /// Gets the payload as a <see cref="ReadOnlyMemory{T}"/>
    /// </summary>
    public ReadOnlyMemory<byte> Memory
    {
        get
        {
            ThrowIfRecycled();
            return new(_oversized, 0, _length);
        }
    }


    /// <summary>
    /// Gets the gRPC marshaller for this type.
    /// </summary>
    public static Marshaller<BytesValue> Marshaller { get; } = new(Serialize, Deserialize);

    private static BytesValue Deserialize(DeserializationContext context)
    {
        try
        {
            var payload = context.PayloadAsReadOnlySequence();
            var totalLen = payload.Length;
            BytesValue? result;

            if (payload.First.Length >= 4)
            {
                // enough bytes in the first segment
                result = TryFastParse(payload.First.Span, payload);
            }
            else
            {
                // copy up-to 4 bytes into a buffer, handling multi-segment concerns
                Span<byte> buffer = stackalloc byte[4];
                payload.Slice(0, (int)Math.Min(totalLen, 4)).CopyTo(buffer);
                result = TryFastParse(buffer, payload);
            }

            return result ?? SlowParse(payload);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Parses any payload <see cref="TryFastParse"/> declines, without going near the runtime model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to be <c>RuntimeTypeModel.Default.Deserialize&lt;BytesValue&gt;(...)</c>, and that
    /// single line was expensive out of all proportion to how often it runs: <c>MarshallerCache</c>
    /// pre-registers <see cref="Marshaller"/> in a field initialiser, so <em>every</em> consumer -
    /// including one that never touches a <see cref="Stream"/>-shaped operation - made the whole
    /// reflective serializer machinery statically reachable. Under native AOT that is metadata ILC
    /// must keep and analyse; measured on a real publish, removing this root and the one in
    /// <c>ProtoBufMarshallerFactory</c> took a sample app from 100 IL warnings to 5. Neither alone
    /// moved it at all - each kept the other's graph alive.
    /// </para>
    /// <para>
    /// The shape is trivial - one <c>bytes</c> field - so parsing it by hand removes the dependency
    /// outright rather than gating it, and it is kept deliberately small: field 1, plus enough
    /// unknown-field skipping not to regress a peer that sends something extra. See
    /// <see cref="Skip"/> for the one capability knowingly dropped.
    /// </para>
    /// <para>
    /// This does not use protobuf-net's own reader APIs either: the assembly compiles against
    /// protobuf-net 2.x (<c>ProtoBufNet2Version</c>), where <c>ProtoReader.State</c> does not exist.
    /// If that pin is ever raised to 3.x, most of this could go.
    /// </para>
    /// <para>
    /// Field 1 uses <b>replace</b> semantics if it somehow appears twice, matching the protobuf rule
    /// for a non-repeated field (last one wins). The runtime model would have <em>concatenated</em>,
    /// via protobuf-net's append behaviour for <c>byte[]</c>; that difference is unreachable in
    /// practice, since each chunk is its own gRPC message and <see cref="Serialize"/> writes field 1
    /// exactly once, so no legacy opt-out is offered.
    /// </para>
    /// </remarks>
    private static BytesValue SlowParse(in ReadOnlySequence<byte> payload)
    {
        if (payload.IsSingleSegment)
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            return Parse(payload.FirstSpan);
#else
            return Parse(payload.First.Span);
#endif
        }

        // multi-segment: linearize into a leased buffer, which is simpler (and cheaper) than
        // threading a segment-crossing reader through what is a handful of bytes of framing
        var length = checked((int)payload.Length);
        var leased = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            payload.CopyTo(leased);
            return Parse(new ReadOnlySpan<byte>(leased, 0, length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(leased);
        }
    }

    private static BytesValue Parse(ReadOnlySpan<byte> payload)
    {
        // an empty payload is a legal encoding of an empty BytesValue - every field defaulted - and
        // is one of the ways TryFastParse declines, so it is a normal outcome rather than an error
        byte[] oversized = [];
        int length = 0;
        bool pooled = false;

        var index = 0;
        while (index < payload.Length)
        {
            var tag = ReadVarint(payload, ref index);
            var field = (int)(tag >> 3);
            var wireType = (int)(tag & 7);

            if (field == 1 && wireType == 2)
            {
                var len = checked((int)ReadVarint(payload, ref index));
                if (len < 0 || index + len > payload.Length) ThrowMalformed();

                if (pooled) ArrayPool<byte>.Shared.Return(oversized); // replace, not append
                if (len == 0)
                {
                    oversized = [];
                    pooled = false;
                }
                else
                {
                    oversized = ArrayPool<byte>.Shared.Rent(len);
                    pooled = true;
                    payload.Slice(index, len).CopyTo(oversized);
                }
                length = len;
                index += len;
            }
            else
            {
                Skip(payload, ref index, wireType);
            }
        }
        return new(oversized, length, pooled);
    }

    /// <summary>
    /// Skips one unknown field.
    /// </summary>
    /// <remarks>
    /// Groups (wire types 3 and 4) are deliberately <em>not</em> supported, and that is the one place
    /// this is less capable than the runtime model it replaced. <see cref="BytesValue"/> is
    /// <c>.google.protobuf.BytesValue</c> - a frozen well-known type with exactly one field - so a
    /// group inside it is not a payload anyone can produce by evolving a schema. Supporting it cost
    /// recursion and a depth counter for a case that cannot arise, so it throws instead.
    /// </remarks>
    private static void Skip(ReadOnlySpan<byte> payload, ref int index, int wireType)
    {
        switch (wireType)
        {
            case 0: // varint
                ReadVarint(payload, ref index);
                break;
            case 1: // fixed64
                index += 8;
                break;
            case 2: // length-delimited
                // NB two statements, deliberately: `index += ReadVarint(payload, ref index)` reads
                // the left operand *before* the call advances `index` through the ref parameter, so
                // the length prefix's own bytes get counted twice and the skip lands short
                var skip = checked((int)ReadVarint(payload, ref index));
                index += skip;
                break;
            case 5: // fixed32
                index += 4;
                break;
            default: // 3/4 groups, 6/7 do not exist
                ThrowMalformed();
                break;
        }
        if (index > payload.Length) ThrowMalformed();
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> payload, ref int index)
    {
        ulong value = 0;
        var shift = 0;
        while (index < payload.Length)
        {
            var b = payload[index++];
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return value;
            shift += 7;
            if (shift > 63) break;
        }
        ThrowMalformed();
        return 0;
    }

    private static void ThrowMalformed()
        => throw new InvalidOperationException($"Invalid {nameof(BytesValue)} payload");

    internal static BytesValue? TryFastParse(ReadOnlySpan<byte> start, in ReadOnlySequence<byte> payload)
    {
        // note: optimized for little-endian CPUs, but safe anywhere (big-endian has an extra reverse)
        int raw = BinaryPrimitives.ReadInt32LittleEndian(start);
        int byteLen, headerLen;
        switch (raw & 0x808080FF) // test the entire first byte, and the MSBs of the rest
        {
            // one-byte length, with anything after (0A00*, backwards)
            case 0x0000000A:
            case 0x8000000A:
            case 0x0080000A:
            case 0x8080000A:
                headerLen = 2;
                byteLen = (raw & 0x7F00) >> 8;
                break;
            // two-byte length, with anything after (0A8000*, backwards)
            case 0x0000800A:
            case 0x8000800A:
                headerLen = 3;
                byteLen = ((raw & 0x7F00) >> 8) | ((raw & 0x7F0000) >> 9);
                break;
            // three-byte length (0A808000, backwards)
            case 0x0080800A:
                headerLen = 4;
                byteLen = ((raw & 0x7F00) >> 8) | ((raw & 0x7F0000) >> 9) | ((raw & 0x7F000000) >> 10);
                break;
            default:
                return null; // not optimized
        }
        if (headerLen + byteLen != payload.Length)
        {
#if DEBUG
            Interlocked.Increment(ref _fastPassMiss);
#endif
            return null; // not the exact payload (other fields?)
        }

#if DEBUG
        // double-check our math using the less efficient library functions
        var arr = start.Slice(0, 4).ToArray();
        Debug.Assert(start[0] == 0x0A, "field 1, string");
        Debug.Assert(Serializer.TryReadLengthPrefix(arr, 1, 3, PrefixStyle.Base128, out int checkLen)
            && checkLen == byteLen, $"length mismatch; {byteLen} vs {checkLen}");
#endif

        var leased = ArrayPool<byte>.Shared.Rent(byteLen);
        payload.Slice(headerLen).CopyTo(leased);
        return new(leased, byteLen, pooled: true);
    }

    private static void Serialize(BytesValue value, global::Grpc.Core.SerializationContext context)
    {
        int byteLen = value.Length, headerLen;
        if (byteLen <= 0x7F) // 7 bit
        {
            headerLen = 2;
        }
        else if (byteLen <= 0x3FFF) // 14 bit
        {
            headerLen = 3;
        }
        else if (byteLen <= 0x1FFFFF) // 21 bit
        {
            headerLen = 4;
        }
        else
        {
            throw new NotSupportedException("We don't expect to write messages this large!");
        }
        int totalLength = headerLen + byteLen;
        context.SetPayloadLength(totalLength);
        var writer = context.GetBufferWriter();
        var buffer = writer.GetSpan(totalLength);
        // we'll assume that we get space for at least the header bytes, but we can *hope* for the entire thing

        buffer[0] = 0x0A; // field 1, string
        switch (headerLen)
        {
            case 2:
                buffer[1] = (byte)byteLen;
                break;
            case 3:
                buffer[1] = (byte)(byteLen | 0x80);
                buffer[2] = (byte)(byteLen >> 7);
                break;
            case 4:
                buffer[1] = (byte)(byteLen | 0x80);
                buffer[2] = (byte)((byteLen >> 7) | 0x80);
                buffer[3] = (byte)(byteLen >> 14);
                break;
        }
        if (buffer.Length >= totalLength)
        {
            // write everything in one go
            value.Span.CopyTo(buffer.Slice(headerLen));
            writer.Advance(totalLength);
        }
        else
        {
            // commit the header, then write the body
            writer.Advance(headerLen);
            writer.Write(value.Span);
        }
        value.Recycle();
        context.Complete();
    }
}