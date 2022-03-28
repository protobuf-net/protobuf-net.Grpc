using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite.Internal.Server;

internal sealed class LiteConnection : IWorker, IConnection
{
    private readonly LiteServer _server;
    private readonly IFrameConnection _connection;
    private readonly ILogger? _logger;
    private string _lastKnownUserAgent = "";
    private readonly ConcurrentDictionary<ushort, IStream> _streams = new ConcurrentDictionary<ushort, IStream>();

    RefCountedMemoryPool<byte> IConnection.Pool => RefCountedMemoryPool<byte>.Shared;
    public int Id { get; }

    void IConnection.Remove(ushort id) => _streams.TryRemove(id, out _);

    CancellationToken IConnection.Shutdown => _server.ServerShutdown;

    string IConnection.LastKnownUserAgent
    {
        get => _lastKnownUserAgent;
        set => _lastKnownUserAgent = value;
    }

    void IConnection.Close(Exception? fault)
    {
        // TODO: need a per-stream shutdown source, rather than per-server?
    }

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
        _ = connection.StartWriterAsync(this, out _output, _server.ServerShutdown);
        _logger.Debug(Id, static (state, _) => $"connection {state} initialized");
    }

    private readonly ChannelWriter<(Frame Frame, FrameWriteFlags Flags)> _output;
    ChannelWriter<(Frame Frame, FrameWriteFlags Flags)> IConnection.Output => _output;

    public void Execute()
    {
        _logger.SetSource(LogKind.Server, "connection " + Id);
        _logger.Debug("starting connection executor");
        Complete ??= this.RunAsync(_logger, _server.ServerShutdown);
    }
    internal Task ExecuteDirect()
    {
        Execute();
        return Complete ?? Task.CompletedTask;
    }

    bool IConnection.TryCreateStream(in Frame initialize, ReadOnlyMemory<char> route, [MaybeNullWhen(false)] out IStream stream)
    {
        if (_server.TryCreateStream(route, out var serverStream) && serverStream is not null)
        {
            serverStream.Initialize(initialize.GetHeader().StreamId, this, _logger);
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
