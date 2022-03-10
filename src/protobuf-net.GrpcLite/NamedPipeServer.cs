using Microsoft.Extensions.Logging;
using System.IO.Pipes;

namespace ProtoBuf.Grpc.Lite
{
    public sealed class NamedPipeServer : StreamServer
    {
        public NamedPipeServer(ILogger? logger) : base(logger) { }
        internal async Task ListenOneAsync(string pipeName, CancellationToken cancellationToken)
        {
            using var stream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.WriteThrough | PipeOptions.Asynchronous);

            Logger?.Log(LogLevel.Debug, default(EventId), pipeName, null, static (state, ex) => $"waiting for connection... {state}");
            await stream.WaitForConnectionAsync(cancellationToken);
            Logger?.Log(LogLevel.Debug, default(EventId), pipeName, null, static (state, ex) => $"client connected to {state}");
            using var connection = AddConnection(stream, stream, cancellationToken);
            using var ctr = cancellationToken.Register(static state => ((StreamServerConnection)state!).Dispose(), connection);
            Logger?.Log(LogLevel.Debug, default(EventId), pipeName, null, static (state, ex) => $"handed off to connection {state}");
            await connection.Complete;
            Logger?.Log(LogLevel.Debug, default(EventId), pipeName, null, static (state, ex) => $"exited {state}");
        }
    }
}
