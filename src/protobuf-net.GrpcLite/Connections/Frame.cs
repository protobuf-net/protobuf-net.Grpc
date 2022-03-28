using ProtoBuf.Grpc.Lite.Internal;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ProtoBuf.Grpc.Lite.Connections;

/// <summary>
/// Represents a gRPC header and payload
/// </summary>
public readonly partial struct Frame
{
    /// <inheritdoc/>
    public override string ToString() => GetHeader().ToString();

    /// <inheritdoc/>
    public override int GetHashCode() => GetHeader().GetHashCode();

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is Frame other && GetHeader().Equals(other.GetHeader());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AssertValidLength(ushort payloadLength)
    {
        if (payloadLength > FrameHeader.MaxPayloadLength) ThrowOversized(payloadLength);
        static void ThrowOversized(ushort length) => throw new InvalidOperationException($"The declared payload length {length} exceeds the permitted maxiumum length of {FrameHeader.MaxPayloadLength}");
    }

    /// <summary>
    /// Create a new <see cref="Frame"/> from an existing buffer; this is a zero-copy operation - the buffer now belongs to the new frame.
    /// </summary>
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

    /// <summary>
    /// Is the <see cref="Frame"/> well-defined, i.e. has a complete header an a valid <see cref="FrameKind"/>.
    /// </summary>
    public bool HasValue
    {
        get
        {
            // needs to be sensibly sized, and the Kind byte (first) must be non-zero
            return _buffer.Length >= FrameHeader.Size && _buffer.Span[0] != 0;
        }
    }

    /// <summary>
    /// Parses the header portion of this frame.
    /// </summary>
    public FrameHeader GetHeader()
        => _buffer.Length >= FrameHeader.Size ? FrameHeader.ReadUnsafe(in _buffer.Span[0]) : default;

    /// <summary>
    /// Obtains the payload portion of this frame.
    /// </summary>
    public ReadOnlyMemory<byte> GetPayload()
        => _buffer.Length >= FrameHeader.Size ? _buffer.Slice(start: FrameHeader.Size) : default;

    /// <summary>
    /// Obtain the head and payload from this frame.
    /// </summary>
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

    /// <summary>
    /// If using a ref-counted memory manager: signal that this memory is no longer required.
    /// </summary>
    public void Release()
    {
        if (MemoryMarshal.TryGetMemoryManager<byte, RefCountedMemoryManager<byte>>(_buffer, out var manager))
            manager.Dispose();
    }

    /// <summary>
    /// If using a ref-counted memory manager: signal that this memory required for an extended duration (must be paired with an additional call to <see cref="Release"/>).
    /// </summary>
    public void Preserve()
    {
        if (MemoryMarshal.TryGetMemoryManager<byte, RefCountedMemoryManager<byte>>(_buffer, out var manager))
            manager.Preserve();
    }

    internal static Frame CreateFrame(RefCountedMemoryPool<byte> pool, FrameHeader frameHeader)
    {
        var memory = pool.RentMemory(FrameHeader.Size);
        var buffer = memory.Slice(start: 0, length: FrameHeader.Size);
        frameHeader.UnsafeWrite(ref buffer.Span[0]);
        var result = new Frame(buffer);
        result.Preserve();
        pool.Return(memory.Slice(start: FrameHeader.Size));
        return result;
    }
}