using ProtoBuf.Grpc.Lite.Internal;
using System.Buffers;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Connections;

public interface IFrameConnection : IAsyncEnumerable<Frame>, IAsyncDisposable
{
    Task WriteAsync(ChannelReader<Frame> source, CancellationToken cancellationToken = default);
}

public static class FrameConnectionExtensions
{
    [Obsolete("this is not the way")]
    public static ValueTask WriteAsync(this ChannelWriter<Frame> output, FrameHeader frame, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!frame.IsFinal || frame.PayloadLength != 0) throw new InvalidOperationException("Payload should be empty and the frame should be final");
            var oversized = ArrayPool<byte>.Shared.Rent(FrameHeader.Size);
            frame.UnsafeWrite(ref oversized[0]);
            var fullFrame = new Frame(new ReadOnlyMemory<byte>(oversized, 0, FrameHeader.Size));
            
            if (output.TryWrite(fullFrame)) return default;
            var pending = output.WriteAsync(fullFrame, cancellationToken);
            if (pending.IsCompleted)
            {
                pending.GetAwaiter().GetResult();
                ArrayPool<byte>.Shared.Return(oversized);
                return default;
            }
            else
            {
                return Awaited(pending, oversized);
            }
        }
        catch(Exception ex)
        {
            return ex.AsValueTask();
        }

        static async ValueTask Awaited(ValueTask pending, byte[] oversized)
        {
            await pending;
            ArrayPool<byte>.Shared.Return(oversized);
        }
    }
}