using ProtoBuf.Grpc.Lite.Internal;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

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
        Buffer = buffer;

        static void ThrowTooSmall() => throw new ArgumentException($"The buffer must include at least {FrameHeader.Size} bytes for the frame header", nameof(buffer));
        static void ThrowLengthMismatch() => throw new ArgumentException("The length of the buffer must match the declared length in the frame header, plus the size of the frame header itself", nameof(buffer));
    }

    public ReadOnlyMemory<byte> Buffer { get; }
    public FrameHeader GetHeader() => FrameHeader.ReadUnsafe(in Buffer.Span[0]); // length checked in .ctor
    public ReadOnlyMemory<byte> GetPayload() => Buffer.Slice(start: FrameHeader.Size);

    public void Deconstruct(out FrameHeader header, out ReadOnlyMemory<byte> payload)
    {
        header = GetHeader();
        payload = GetPayload();
    }

    public void Release()
    {
        if (MemoryMarshal.TryGetMemoryManager<byte, FrameBufferManager.Slab>(Buffer, out var slab))
            slab.RemoveReference();
    }
}