using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ProtoBuf.Grpc.Lite.Internal.Server
{
    internal sealed class LiteConnection : IWorker, IListener, IStreamOwner
    {
        private readonly LiteServer _server;
        private readonly IFrameConnection _connection;
        private readonly ILogger? _logger;

        private readonly ConcurrentDictionary<ushort, IStream> _streams = new ConcurrentDictionary<ushort, IStream>();

        public int Id { get; }

        void IStreamOwner.Remove(ushort id) => _streams.Remove(id, out _);

        CancellationToken IStreamOwner.Shutdown => _server.ServerShutdown;

        // will be null if not started
        public Task? Complete { get; private set; }

        bool IListener.IsClient => false;

        IFrameConnection IListener.Connection => _connection;

        ConcurrentDictionary<ushort, IStream> IListener.Streams => _streams;

        public LiteConnection(LiteServer server, IFrameConnection connection, ILogger? logger)
        {
            _connection = connection;
            _logger = logger ?? server.Logger;
            Id = server.NextStreamId();
            _server = server;

            _logger.Debug(Id, static (state, _) => $"connection {state} initialized");
        }

        public void Execute()
        {
            _logger.SetSource(LogKind.Server, "connection " + Id);
            _logger.Debug("starting connection executor");
            Complete ??= this.RunAsync(_logger, _server.ServerShutdown);
        }

        bool IListener.TryCreateStream(in Frame initialize, [MaybeNullWhen(false)] out IStream handler)
        {
            if (_server.TryGetHandler(initialize.GetPayloadString(), out var serverHandler))
            {
                serverHandler.Initialize(initialize.GetHeader().StreamId, _connection, _logger, this);
                handler = serverHandler;
                return handler is not null;
            }
            else
            {
                handler = null;
                return false;
            }
        }
    }
}
