using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Internal.Connections;
using System.IO.Pipelines;

namespace ProtoBuf.Grpc.Lite.Connections;

public static class PipeExtensions
{
    public static IFrameConnection CreateFrameConnection(this IDuplexPipe pipe, ILogger? logger = null)
        => new PipeFrameConnection(pipe, logger);
}
