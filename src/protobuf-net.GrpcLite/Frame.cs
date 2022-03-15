using ProtoBuf.Grpc.Lite.Internal;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace ProtoBuf.Grpc.Lite;

public readonly struct Frame
{
    public static bool TryRead(in ReadOnlyMemory<byte> buffer, out Frame frame, out int bytesRead)
    {
        var bufferLength = buffer.Length;
        if (bufferLength >= FrameHeader.Size)
        {
            var declaredLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Span.Slice(FrameHeader.PayloadLengthOffset));
            AssertValidLength(declaredLength);
            bytesRead = FrameHeader.Size + declaredLength;
            if (bufferLength >= bytesRead)
            {
                frame = new Frame(buffer.Slice(0, bytesRead), true);
                return true;
            }
        }
        bytesRead = 0;
        frame = default;
        return false;
    }

    public override string ToString() => GetHeader().ToString();

    public override int GetHashCode() => GetHeader().GetHashCode();

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is Frame other && GetHeader().Equals(other.GetHeader());

    internal static void AssertValidLength(ushort length)
    {
        if (length > FrameHeader.MaxPayloadSize) ThrowOversized(length);
        static void ThrowOversized(ushort length) => throw new InvalidOperationException($"The declared payload length {length} exceeds the permitted maxiumum length of {FrameHeader.MaxPayloadSize}");
    }
    public Frame(in ReadOnlyMemory<byte> buffer) : this(buffer, false) { }
    internal Frame(in ReadOnlyMemory<byte> buffer, bool trusted)
    {
#if DEBUG
        trusted = false; // always validate in debug
#endif
        if (!trusted)
        {
            if (buffer.Length < FrameHeader.Size) ThrowTooSmall();
            var declaredLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Span.Slice(FrameHeader.PayloadLengthOffset));
            AssertValidLength(declaredLength);
            if (buffer.Length != FrameHeader.Size + declaredLength) ThrowLengthMismatch();
        }
        _buffer = buffer;

        static void ThrowTooSmall() => throw new ArgumentException($"The buffer must include at least {FrameHeader.Size} bytes for the frame header", nameof(buffer));
        static void ThrowLengthMismatch() => throw new ArgumentException("The length of the buffer must match the declared length in the frame header, plus the size of the frame header itself", nameof(buffer));
    }

    private readonly ReadOnlyMemory<byte> _buffer;
    internal ReadOnlyMemory<byte> RawBuffer => _buffer;

    /// <summary>
    /// The length of the frame inscluding the header
    /// </summary>
    public int TotalLength => _buffer.Length;
    public bool HasValue
    {
        get
        {
            // needs to be sensibly sized, and the Kind byte (first) must be non-zero
            return _buffer.Length >= FrameHeader.Size && _buffer.Span[0] != 0;
        }
    }

    public FrameHeader GetHeader()
        => _buffer.Length >= FrameHeader.Size ? FrameHeader.ReadUnsafe(in _buffer.Span[0]) : default;

    public ReadOnlyMemory<byte> GetPayload()
        => _buffer.Length >= FrameHeader.Size ? _buffer.Slice(start: FrameHeader.Size) : default;

    public void Deconstruct(out FrameHeader header, out ReadOnlyMemory<byte> payload)
    {
        if (_buffer.Length >= FrameHeader.Size)
        {
            header = FrameHeader.ReadUnsafe(in _buffer.Span[0]);
            payload = _buffer.Slice(start: FrameHeader.Size);
        }
        else
        {
            header = default;
            payload = default;
        }
    }

    public void Release()
    {
        if (MemoryMarshal.TryGetMemoryManager<byte, FrameBufferManager.Slab>(_buffer, out var slab))
            slab.RemoveReference();
    }

    public void Preserve()
    {
        if (MemoryMarshal.TryGetMemoryManager<byte, FrameBufferManager.Slab>(_buffer, out var slab))
            slab.AddReference();
    }

    internal string GetPayloadString(Encoding? encoding = null)
    {
        var payload = GetPayload();
        return payload.IsEmpty ? "" : (encoding ?? Encoding.UTF8).GetString(payload.Span);
    }
}