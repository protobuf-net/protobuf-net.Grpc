using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Internal.Connections;
using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite.Connections;

/// <summary>
/// Utility methods for working with <see cref="IDuplexPipe"/> ("pipelines") with gRPC.
/// </summary>
public static class PipeExtensions
{
    /// <summary>
     /// Creates a <see cref="Frame"/> processor over a <see cref="IDuplexPipe"/>.
     /// </summary>
    public static IFrameConnection AsFrames(this IDuplexPipe pipe, ILogger? logger = null)
        => new PipeFrameConnection(pipe, logger);

    /// <summary>
    /// Creates a <see cref="Frame"/> processor over a <see cref="IDuplexPipe"/>.
    /// </summary>
    public static Func<CancellationToken, ValueTask<ConnectionState<IFrameConnection>>> AsFrames(
        this Func<CancellationToken, ValueTask<ConnectionState<IDuplexPipe>>> factory) => async cancellationToken =>
    {
        var source = await factory(cancellationToken);
        try
        {
            return source.ChangeType<IFrameConnection>(new PipeFrameConnection(source.Value, source.Logger));
        }
        catch (Exception ex)
        {
            try
            {
                await source.Value.Output.CompleteAsync(ex);
            }
            catch { }
            throw;
        }
    };

    /// <summary>
    /// Listen for new connection requests on the designated endpoint.
    /// </summary>
    public static Task ListenAsync(this LiteServer server, Func<CancellationToken, ValueTask<ConnectionState<IDuplexPipe>>> listener)
        => server.ListenAsync(listener.AsFrames());
}
