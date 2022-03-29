using ProtoBuf.Grpc.Lite.Connections;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProtoBuf.Grpc.Lite.Internal.Connections;

internal sealed class FrameSequenceSegment : ReadOnlySequenceSegment<byte>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySequence<byte> Create(in ReadOnlySpan<Frame> frames)
    {
        var result = CreateCore(frames);
        Debug.Assert(result.Length == frames.PayloadLength(), $"FrameSequenceSegment length mismatch: {result.Length} vs {frames.PayloadLength()}; {frames.Length} buffers");
        return result;
    }
    private static ReadOnlySequence<byte> CreateCore(in ReadOnlySpan<Frame> frames)
    {
        switch (frames.Length)
        {
            case 0:
                return default;
            case 1:
                var buffer = frames[0].GetPayload();
                return buffer.IsEmpty ? default : buffer.AsReadOnlySequence();
        }

        var iter = frames.GetEnumerator();
        iter.MoveNext();
        FrameSequenceSegment? first = null, last = null;
        foreach (var frame in frames)
        {
            var payload = frame.GetPayload(); // don't bother checking length; we don't *expect* any empties
            if (!payload.IsEmpty)
            {
                // since there are headers between payloads, we are never
                // able to merge frames to make a larger payload buffer; that's fine
                last = new FrameSequenceSegment(last, payload);
                if (first is null) first = last;
            }
        }

        if (first is null) return default;
#if !NET472 // can't optimize this in netfx due to ROS bug; need to use full version
        if (ReferenceEquals(first, last)) return new ReadOnlySequence<byte>(first.Memory);
#endif
        return new ReadOnlySequence<byte>(first, 0, last!, last!.Memory.Length);
    }


#if NET472
    // a bug in NETFX means ROS doesn't work well with custom memory managers and non-zero offsets; so:
    // we'll store the *original* memory (for recycling purposes) here, and expose just the array for ROS
    // to touch
    public ReadOnlyMemory<byte> OriginalMemory { get; }

#else
    public ReadOnlyMemory<byte> OriginalMemory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Memory;
    }
#endif
    private FrameSequenceSegment(FrameSequenceSegment? previous, ReadOnlyMemory<byte> memory) : base()
    {
#if NET472 // a bug in NETFX means ROS doesn't work well with custom memory managers and non-zero offsets
        OriginalMemory = memory;
        // fortunately, our custom memory manager is happy to expose the underlying array, so we can use
        // that in the ROS
        Memory = MemoryMarshal.TryGetArray(memory, out var segment)
            ? new ReadOnlyMemory<byte>(segment.Array, segment.Offset, segment.Count) : memory;
#else
        Memory = memory;
#endif
        Next = null;
        if (previous is not null)
        {
            previous.Next = this;
            RunningIndex = previous.RunningIndex + previous.Memory.Length;
        }
        else
        {
            RunningIndex = 0;
        }
    }


}
