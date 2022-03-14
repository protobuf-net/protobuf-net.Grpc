using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal.Server;
using System.Collections.Concurrent;
using System.Text;

namespace ProtoBuf.Grpc.Lite.Internal
{
    public sealed class LiteServerConnection : IAsyncDisposable, IDisposable
    {
        public int Id { get; }
        public Task Complete => _connection.Complete;

        private readonly LiteServer _server;
        private readonly IFrameConnection _connection;
        private readonly ILogger? _logger;

        public LiteServerConnection(LiteServer server, IFrameConnection connection, ILogger? logger)
        {
            _connection = connection;
            _logger = logger ?? server.Logger;
            Id = server.NextId();
            _server = server;

            _logger.LogDebug(Id, static (state, _) => $"connection {state} initialized");
        }

        private readonly ConcurrentDictionary<ushort, IServerHandler> _activeOperations = new ConcurrentDictionary<ushort, IServerHandler>();

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Yield();
                _logger.LogDebug(Id, static (state, _) => $"connection {state} processing streams...");
                await using var iter = _connection.GetAsyncEnumerator(cancellationToken);
                while (!cancellationToken.IsCancellationRequested && await iter.MoveNextAsync())
                {
                    var frame = iter.Current;
                    var header = frame.GetHeader();
                    _logger.LogDebug(frame, static (state, _) => $"received frame {state}");
                    bool releaseFrame = false;
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
                            releaseFrame = true;
                            break;
                        case FrameKind.NewStream:
                            var method = Encoding.UTF8.GetString(frame.GetPayload().Span);
                            var handler = _server.TryGetHandler(method, out var handlerFactory) ? handlerFactory() : null;
                            if (handler is null)
                            {
                                _logger.LogDebug(method, static (state, _) => $"method not found: {state}");
                                await _connection.WriteAsync(new FrameHeader(FrameKind.MethodNotFound, 0, header.StreamId, 0), cancellationToken);
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
                                switch (handler.MethodType)
                                {
                                    case MethodType.DuplexStreaming:
                                        handler.BeginBackgroundExecute();
                                        break;
                                }
                            }
                            releaseFrame = true;
                            break;
                        case FrameKind.Payload:
                            if (_activeOperations.TryGetValue(header.StreamId, out handler))
                            {
                                _logger.LogDebug((handler: handler, frame: frame), static (state, _) => $"pushing {state.frame} to {state.handler.Method} ({state.handler.MethodType})");
                                await handler.ReceivePayloadAsync(frame, cancellationToken);
                            }
                            else
                            {
                                _logger.LogInformation(frame, static (state, _) => $"received payload for unknown stream {state}");
                                releaseFrame = true;
                            }
                            break;
                        default:
                            _logger.LogInformation(frame, static (state, _) => $"unexpected frame type {state}");
                            releaseFrame = true;
                            break;
                    }
                    if (releaseFrame)
                    {
                        _logger.LogInformation(frame.Buffer, static (state, _) => $"releasing {state.Length} bytes");
                    }
                }
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken == cancellationToken)
            { } // alt-success
            catch (Exception ex)
            {
                _logger.LogError(ex);
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
