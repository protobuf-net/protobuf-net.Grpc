using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ProtoBuf.Grpc.Lite.Connections;

public readonly partial struct Frame
{
    public override string ToString() => GetHeader().ToString();

    public override int GetHashCode() => GetHeader().GetHashCode();

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is Frame other && GetHeader().Equals(other.GetHeader());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AssertValidLength(ushort payloadLength)
    {
        if (payloadLength > FrameHeader.MaxPayloadLength) ThrowOversized(payloadLength);
        static void ThrowOversized(ushort length) => throw new InvalidOperationException($"The declared payload length {length} exceeds the permitted maxiumum length of {FrameHeader.MaxPayloadLength}");
    }
    public Frame(in ReadOnlyMemory<byte> buffer)
    {
        Debug.Assert(MemoryMarshal.TryGetMemoryManager(buffer, out RefCountedMemoryManager<byte> _), "should have ref-counted memory manager");
        
        if (buffer.Length < FrameHeader.Size) ThrowTooSmall();
        var declaredLength = FrameHeader.GetPayloadLength(buffer.Span);
        AssertValidLength(declaredLength);
        if (buffer.Length != FrameHeader.Size + declaredLength) ThrowLengthMismatch();
        _buffer = buffer;

        static void ThrowTooSmall() => throw new ArgumentException($"The buffer must include at least {FrameHeader.Size} bytes for the frame header", nameof(buffer));
        static void ThrowLengthMismatch() => throw new ArgumentException("The length of the buffer must match the declared length in the frame header, plus the size of the frame header itself", nameof(buffer));
    }

    private readonly ReadOnlyMemory<byte> _buffer;

    /// <summary>
    /// Gets the entire memory representation for this frame, <em>including the header</em>; to get the payload by itself, use <see cref="GetPayload"/>.
    /// </summary>
    public ReadOnlyMemory<byte> Memory => _buffer;

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
        if (MemoryMarshal.TryGetMemoryManager<byte, RefCountedMemoryManager<byte>>(_buffer, out var manager))
            manager.Dispose();
    }

    public void Preserve()
    {
        if (MemoryMarshal.TryGetMemoryManager<byte, RefCountedMemoryManager<byte>>(_buffer, out var manager))
            manager.Preserve();
    }

    internal string GetPayloadString(Encoding? encoding = null)
    {
        var payload = GetPayload();
        return payload.IsEmpty ? "" : (encoding ?? Encoding.UTF8).GetString(payload.Span);
    }
}