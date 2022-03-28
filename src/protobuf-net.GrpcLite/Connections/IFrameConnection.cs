using ProtoBuf.Grpc.Lite.Internal;
using System.Buffers;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Connections;

/// <summary>
/// Represents a processor capable of reading and writing <see cref="Frame"/> data.
/// </summary>
public interface IFrameConnection : IAsyncEnumerable<Frame>, IAsyncDisposable
{
    /// <summary>
    /// Writes all the frames from the provided channel.
    /// </summary>
    Task WriteAsync(ChannelReader<(Frame Frame, FrameWriteFlags Flags)> source, CancellationToken cancellationToken = default);
}