//using Microsoft.Extensions.Logging;
//using ProtoBuf.Grpc.Lite.Internal;
//using System.IO.Pipes;

//namespace ProtoBuf.Grpc.Lite
//{
//    public sealed class NamedPipeServer : StreamServer
//    {
//        public NamedPipeServer(ILogger? logger = null) : base(logger) { }
//        internal async Task ListenOneAsync(string pipeName, CancellationToken cancellationToken)
//        {
//            using var stream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.WriteThrough | PipeOptions.Asynchronous);

//            try
//            {
//                Logger.LogDebug(pipeName, static (state, _) => $"waiting for connection... {state}");
//                await stream.WaitForConnectionAsync(cancellationToken);
//                Logger.LogDebug(pipeName, static (state, _) => $"client connected to {state}");

//                var conn = new SynchronizedGate(new StreamFrameConnection(stream, stream), 0);

//                await using var connection = AddConnection(conn, cancellationToken);
//                using var ctr = cancellationToken.Register(static state => ((StreamServerConnection)state!).Dispose(), connection);
//                Logger.LogDebug(pipeName, static (state, _) => $"handed off to connection {state}");
//                await connection.Complete;
//                Logger.LogDebug(pipeName, static (state, _) => $"exited {state}");
//            }
//            catch (Exception ex)
//            {
//                Logger.LogError(ex);
//            }
//        }
//    }
//}
