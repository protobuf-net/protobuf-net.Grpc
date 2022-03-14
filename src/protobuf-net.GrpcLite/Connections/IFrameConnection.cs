using ProtoBuf.Grpc.Lite.Internal;
using System.Buffers;

namespace ProtoBuf.Grpc.Lite.Connections;

public interface IFrameConnection : IAsyncEnumerable<Frame>, IAsyncDisposable
{
    ValueTask WriteAsync(Frame frame, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(ReadOnlyMemory<Frame> frames, CancellationToken cancellationToken = default);
    bool ThreadSafeWrite { get; }
    void Close(Exception? exception = null);
    Task Complete { get; }
}

public static class FrameConnectionExtensions
{
    public static ValueTask WriteAsync(this IFrameConnection connection, FrameHeader frame, CancellationToken cancellationToken = default)
    {
        try
        {
            var oversized = ArrayPool<byte>.Shared.Rent(FrameHeader.Size);
            frame.UnsafeWrite(ref oversized[0]);
            var fullFrame = new Frame(new ReadOnlyMemory<byte>(oversized, 0, FrameHeader.Size)); // note that this will check the payload length is correct (i.e. zero)
            var pending = connection.WriteAsync(fullFrame, cancellationToken);
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