using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Internal;
using ProtoBuf.Grpc.Lite.Internal.Server;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite
{
    public sealed class StreamServerConnection : IDisposable
    {
        public int Id { get; }

        private readonly StreamServer _server;
        private readonly Stream _input;
        private readonly Stream _output;
        private readonly Channel<StreamFrame> _outbound;
        private readonly ILogger? _logger;

        public Task Complete { get; }

        public StreamServerConnection(StreamServer server, Stream input, Stream output, CancellationToken cancellationToken)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));
            if (output is null) throw new ArgumentNullException(nameof(output));
            if (!input.CanRead) throw new ArgumentException("Cannot read from input stream", nameof(input));
            if (!output.CanWrite) throw new ArgumentException("Cannot write to output stream", nameof(output));
            _input = input;
            _output = output;
            _logger = server.Logger;
            Id = server.NextId();
            _server = server;

            _outbound = StreamFrame.CreateChannel();
            _logger.LogDebug(Id, static (state, _) => $"connection {state} initialized; processing streams...");
            Complete = StreamFrame.WriteFromOutboundChannelToStream(_outbound, output, _logger, cancellationToken);

            _ = ConsumeAsync(cancellationToken);
        }

        private readonly ConcurrentDictionary<ushort, IServerHandler> _activeOperations = new ConcurrentDictionary<ushort, IServerHandler>();

        async Task ConsumeAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            while (!cancellationToken.IsCancellationRequested)
            {
                var frame = await StreamFrame.ReadAsync(_input, cancellationToken);
                _logger.LogDebug(frame, static (state, _) => $"received frame {state}");
                switch (frame.Kind)
                {
                    case FrameKind.Close:
                    case FrameKind.Ping:
                        var generalFlags = (GeneralFlags)frame.KindFlags;
                        if ((generalFlags & GeneralFlags.IsResponse) == 0)
                        {
                            // if this was a request, we reply in kind, but noting that it is a response
                            await _outbound.Writer.WriteAsync(new StreamFrame(frame.Kind, frame.RequestId, (byte)GeneralFlags.IsResponse), cancellationToken);
                        }
                        // shutdown if requested
                        if (frame.Kind == FrameKind.Close)
                        {
                            _outbound.Writer.TryComplete();
                        }
                        break;
                    case FrameKind.NewUnary:
                    case FrameKind.NewClientStreaming:
                    case FrameKind.NewServerStreaming:
                    case FrameKind.NewDuplex:
                        var method = Encoding.UTF8.GetString(frame.Buffer, frame.Offset, frame.Length);
                        var handler = _server.TryGetHandler(method, out var handlerFactory) ? handlerFactory() : null;
                        if (handler is null)
                        {
                            _logger.LogDebug(method, static (state, _) => $"method not found: {state}");
                            await _outbound.Writer.WriteAsync(new StreamFrame(FrameKind.MethodNotFound, frame.RequestId, 0), cancellationToken);
                        }
                        else if (handler.Kind != frame.Kind)
                        {
                            _logger.LogInformation((Handler: handler, Received: frame.Kind), static (state, _) => $"invalid method kind: expected {state.Handler.Kind}, received {state.Received}; {state.Handler.Method}");
                            await _outbound.Writer.WriteAsync(new StreamFrame(FrameKind.Cancel, frame.RequestId, 0), cancellationToken);
                        }
                        else if (!_activeOperations.TryAdd(frame.RequestId, handler))
                        {
                            _logger.LogError(frame.RequestId, static (state, _) => $"duplicate id! {state}");
                            await _outbound.Writer.WriteAsync(new StreamFrame(FrameKind.Cancel, frame.RequestId, 0), cancellationToken);
                        }
                        else
                        {
                            handler.Initialize(frame.RequestId, _outbound, _logger);
                            _logger.LogDebug(method, static (state, _) => $"method accepted: {state}");


                            // intiate the server request
                            switch (frame.Kind)
                            {
                                case FrameKind.NewDuplex:
                                    handler.Execute();
                                    break;
                            }
                        }

                        break;
                    case FrameKind.Payload:
                        if (_activeOperations.TryGetValue(frame.RequestId, out handler))
                        {
                            await handler.ReceivePayloadAsync(frame, cancellationToken);
                        }
                        break;
                }
            }
        }



        public void Dispose()
        {
            _outbound.Writer.TryComplete();
            StreamChannel.Dispose(_input, _output);
        }
        public ValueTask DisposeAsync()
        {
            _outbound.Writer.TryComplete();
            return StreamChannel.DisposeAsync(_input, _output);
        }
    }
}
