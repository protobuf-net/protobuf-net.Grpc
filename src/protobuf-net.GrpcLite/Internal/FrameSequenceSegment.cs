using System.Buffers;

namespace ProtoBuf.Grpc.Lite.Internal
{
    internal sealed class FrameSequenceSegment : ReadOnlySequenceSegment<byte>
    {
        public static ReadOnlySequence<byte> Create(in ReadOnlySpan<Frame> frames)
        {
            switch (frames.Length)
            {
                case 0:
                    return default;
                case 1:
                    var buffer = frames[0].GetPayload();
                    return buffer.IsEmpty ? default : new ReadOnlySequence<byte>(buffer);
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
            if (ReferenceEquals(first, last)) return new ReadOnlySequence<byte>(first.Memory);
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

        internal static object Create(ReadOnlyMemory<Frame> frameGroup)
        {
            throw new NotImplementedException();
        }
    }
}
