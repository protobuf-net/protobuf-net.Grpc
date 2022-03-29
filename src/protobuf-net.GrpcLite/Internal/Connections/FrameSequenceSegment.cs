using ProtoBuf.Grpc.Lite.Connections;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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
#if !NET472 // avoid this optimization on netfx; due to the ROS bug, this might end up allocating a second ReadOnlySequenceSegment (if no array support)
        if (ReferenceEquals(first, last)) return first.Memory.AsReadOnlySequence();
#endif
        return new ReadOnlySequence<byte>(first, 0, last!, last!.Memory.Length);
    }

    private FrameSequenceSegment(FrameSequenceSegment? previous, ReadOnlyMemory<byte> memory) : base()
    {
        Memory = memory;
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
