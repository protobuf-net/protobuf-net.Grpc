using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal.Server
{
    internal sealed class LiteConnection : IWorker, IConnection
    {
        private readonly LiteServer _server;
        private readonly IFrameConnection _connection;
        private readonly ILogger? _logger;

        private readonly ConcurrentDictionary<ushort, IStream> _streams = new ConcurrentDictionary<ushort, IStream>();

        public int Id { get; }

        void IConnection.Remove(ushort id) => _streams.Remove(id, out _);

        CancellationToken IConnection.Shutdown => _server.ServerShutdown;

        // will be null if not started
        public Task? Complete { get; private set; }

        bool IConnection.IsClient => false;

        IAsyncEnumerable<Frame> IConnection.Input => _connection;

        ConcurrentDictionary<ushort, IStream> IConnection.Streams => _streams;

        public LiteConnection(LiteServer server, IFrameConnection connection, ILogger? logger)
        {
            _connection = connection;
            _logger = logger ?? server.Logger;
            Id = server.NextStreamId();
            _server = server;
            _ = connection.StartWriterAsync(false, out _output, _server.ServerShutdown);
            _logger.Debug(Id, static (state, _) => $"connection {state} initialized");
        }

        private readonly ChannelWriter<Frame> _output;
        ChannelWriter<Frame> IConnection.Output => _output;

        public void Execute()
        {
            _logger.SetSource(LogKind.Server, "connection " + Id);
            _logger.Debug("starting connection executor");
            Complete ??= this.RunAsync(_logger, _server.ServerShutdown);
        }

        bool IConnection.TryCreateStream(in Frame initialize, [MaybeNullWhen(false)] out IStream stream)
        {
            if (_server.TryCreateStream(initialize.GetPayloadString(), out var serverStream) && serverStream is not null)
            {
                serverStream.Initialize(initialize.GetHeader().StreamId, _output, _logger, this);
                stream = serverStream;
                return true;
            }
            else
            {
                stream = null;
                return false;
            }
        }
    }
}
