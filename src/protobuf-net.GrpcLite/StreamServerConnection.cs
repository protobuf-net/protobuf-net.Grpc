using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Internal;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite
{
    public sealed class StreamServerConnection : IDisposable
    {
        public int Id { get; }

        public Task Complete { get; }

        private Stream _input, _output;
        private readonly Channel<StreamFrame> _outbound;
        private readonly ILogger? _logger;

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
            
            _outbound = StreamFrame.CreateChannel();
            _logger?.Log(LogLevel.Debug, default(EventId), Id, null, static (state, ex) => $"connection {state} initialized; processing streams...");
            Complete = StreamFrame.WriteFromOutboundChannelToStream(_outbound, output, _logger, cancellationToken);
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
