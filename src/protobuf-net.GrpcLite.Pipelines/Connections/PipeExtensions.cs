using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Internal.Connections;
using System.IO.Pipelines;

namespace ProtoBuf.Grpc.Lite.Connections;

public static class PipeExtensions
{
    public static IFrameConnection AsFrames(this IDuplexPipe pipe, ILogger? logger = null)
        => new PipeFrameConnection(pipe, logger);

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

    public static Task ListenAsync(this LiteServer server, Func<CancellationToken, ValueTask<ConnectionState<IDuplexPipe>>> listener)
        => server.ListenAsync(listener.AsFrames());
}
