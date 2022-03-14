using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal;
using ProtoBuf.Grpc.Lite.Internal.Server;
using System.Collections.Concurrent;
using System.Text;

namespace ProtoBuf.Grpc.Lite
{
    public sealed class StreamServerConnection : IAsyncDisposable, IDisposable
    {
        public int Id { get; }
        public Task Complete => _connection.Complete;

        private readonly StreamServer _server;
        private readonly IFrameConnection _connection;
        private readonly ILogger? _logger;

        public StreamServerConnection(StreamServer server, IFrameConnection connection, CancellationToken cancellationToken)
        {
            _connection = connection;
            _logger = server.Logger;
            Id = server.NextId();
            _server = server;

            
            _logger.LogDebug(Id, static (state, _) => $"connection {state} initialized; processing streams...");

            _ = ConsumeAsync(cancellationToken);
        }

        private readonly ConcurrentDictionary<ushort, IServerHandler> _activeOperations = new ConcurrentDictionary<ushort, IServerHandler>();

        async Task ConsumeAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            var iter = _connection.GetAsyncEnumerator(cancellationToken);
            while (!cancellationToken.IsCancellationRequested && await iter.MoveNextAsync())
            {
                var frame = iter.Current;
                var header = frame.GetHeader();
                _logger.LogDebug(frame, static (state, _) => $"received frame {state}");
                switch (header.Kind)
                {
                    case FrameKind.Close:
                    case FrameKind.Ping:
                        var generalFlags = (GeneralFlags)header.KindFlags;
                        if ((generalFlags & GeneralFlags.IsResponse) == 0)
                        {
                            // if this was a request, we reply in kind, but noting that it is a response
                            await _connection.WriteAsync(new FrameHeader(header.Kind, (byte)GeneralFlags.IsResponse, header.StreamId, header.SequenceId), cancellationToken);
                        }
                        // shutdown if requested
                        if (header.Kind == FrameKind.Close)
                        {
                            _connection.Close();
                        }
                        break;
                    case FrameKind.Unary:
                    case FrameKind.ClientStreaming:
                    case FrameKind.ServerStreaming:
                    case FrameKind.DuplexStreaming:
                        var method = Encoding.UTF8.GetString(frame.GetPayload().Span);
                        var handler = _server.TryGetHandler(method, out var handlerFactory) ? handlerFactory() : null;
                        if (handler is null)
                        {
                            _logger.LogDebug(method, static (state, _) => $"method not found: {state}");
                            await _connection.WriteAsync(new FrameHeader(FrameKind.MethodNotFound, 0, header.StreamId, 0), cancellationToken);
                        }
                        else if (handler.Kind != header.Kind)
                        {
                            _logger.LogInformation((Handler: handler, Received: header.Kind), static (state, _) => $"invalid method kind: expected {state.Handler.Kind}, received {state.Received}; {state.Handler.Method}");
                            await _connection.WriteAsync(new FrameHeader(FrameKind.Cancel, 0, header.StreamId, handler.NextSequenceId()), cancellationToken);
                        }
                        else if (!_activeOperations.TryAdd(header.StreamId, handler))
                        {
                            _logger.LogError(header.StreamId, static (state, _) => $"duplicate id! {state}");
                            await _connection.WriteAsync(new FrameHeader(FrameKind.Cancel, 0, header.StreamId, handler.NextSequenceId()), cancellationToken);
                        }
                        else
                        {
                            handler.Initialize(header.StreamId, _connection, _logger);
                            _logger.LogDebug(method, static (state, _) => $"method accepted: {state}");


                            // intiate the server request
                            switch (header.Kind)
                            {
                                case FrameKind.DuplexStreaming:
                                    handler.BeginBackgroundExecute();
                                    break;
                            }
                        }

                        break;
                    case FrameKind.Payload:
                        if (_activeOperations.TryGetValue(header.StreamId, out handler))
                        {
                            await handler.ReceivePayloadAsync(frame, cancellationToken);
                        }
                        break;
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            _connection?.Close();
            return Utilities.SafeDisposeAsync(_connection);
        }

        public void Dispose()
        {
            _connection?.Close();
            _ = Utilities.SafeDisposeAsync(_connection).AsTask();
        }
    }
}
