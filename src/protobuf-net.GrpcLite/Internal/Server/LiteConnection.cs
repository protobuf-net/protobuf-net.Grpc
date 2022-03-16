using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ProtoBuf.Grpc.Lite.Internal.Server
{
    internal sealed class LiteConnection : IWorker, IListener
    {
        private readonly LiteServer _server;
        private readonly IFrameConnection _connection;
        private readonly ILogger? _logger;

        private readonly ConcurrentDictionary<ushort, IStream> _streams = new ConcurrentDictionary<ushort, IStream>();

        public int Id { get; }

        // will be null if not started
        public Task? Complete { get; private set; }

        bool IListener.IsClient => false;

        IFrameConnection IListener.Connection => _connection;

        ConcurrentDictionary<ushort, IStream> IListener.Streams => _streams;

        public LiteConnection(LiteServer server, IFrameConnection connection, ILogger? logger)
        {
            _connection = connection;
            _logger = logger ?? server.Logger;
            Id = server.NextId();
            _server = server;

            _logger.Debug(Id, static (state, _) => $"connection {state} initialized");
        }

        public void Execute()
        {
            Logging.SetSource(Logging.ServerPrefix + "connection " + Id);
            Complete ??= this.RunAsync(_logger, _server.ServerShutdown);
        }

        bool IListener.TryCreateStream(in Frame initialize, [MaybeNullWhen(false)] out IStream handler)
        {
            if (_server.TryGetHandler(initialize.GetPayloadString(), out var serverHandler))
            {
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
