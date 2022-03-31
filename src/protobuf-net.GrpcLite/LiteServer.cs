using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal;
using ProtoBuf.Grpc.Lite.Internal.Connections;
using ProtoBuf.Grpc.Lite.Internal.Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite;

/// <summary>
/// A lightweight gRPC server implementation.
/// </summary>
public sealed class LiteServer : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Create a new instance.
    /// </summary>
    public LiteServer(ILogger? logger = null)
        => Logger = logger;

    internal readonly ILogger? Logger;

    private int id = -1;
    internal int NextStreamId() => Interlocked.Increment(ref id);

    internal CancellationToken ServerShutdown => _serverShutdown.Token;

    CancellationTokenSource _serverShutdown = new CancellationTokenSource();

    /// <summary>
    /// Stop the server and all streams being serviced by this server.
    /// </summary>
    public void Stop() => _serverShutdown.Cancel();

    void IDisposable.Dispose() => Stop();
    ValueTask IAsyncDisposable.DisposeAsync()
    {
        Stop();
        return default;
    }

    /// <summary>
    /// Create an in-process client channel to this server that still uses serialization and routing, but does not go via the network stack etc.
    /// </summary>
    public LiteChannel CreateLocalClient(string? name = null)
    {
        if (string.IsNullOrWhiteSpace(name)) name = "(local)";
        NullConnection.CreateLinkedPair(out var x, out var y);
        var server = new LiteConnection(this, x, Logger);
        var client = new LiteChannel(y, name!, Logger);
        server.StartWorker();
        return client;
    }

    /// <summary>
    /// Listen for new connection requests on the designated endpoint.
    /// </summary>
    public Task ListenAsync(Func<CancellationToken, ValueTask<ConnectionState<IFrameConnection>>> listener)
        => Task.Run(() => ListenAsyncCore(listener));

    /// <summary>
    /// Listen for new connection requests on the designated endpoint.
    /// </summary>
    public Task ListenAsync(Func<CancellationToken, ValueTask<ConnectionState<Stream>>> listener)
        => ListenAsync(listener.AsFrames());

    private async Task ListenAsyncCore(Func<CancellationToken, ValueTask<ConnectionState<IFrameConnection>>> listener)
    {
        Logger.SetSource(LogKind.Server, "listener");
        Logger.Debug("starting listener (accepts incoming connections)");
        try
        {
            while (!_serverShutdown.IsCancellationRequested)
            {
                await Task.Yield(); // let's not hog a core if we have lots of connections...
                Logger.Information("listening for new connection...");
                try
                {
                    var connection = await listener(ServerShutdown);
                    if (connection is null)
                    {
                        continue;
                    }

                    Logger.Information(connection, static (state, _) => $"established connection {state.Name}");
                    var server = new LiteConnection(this, connection.Value, connection.Logger);
                    server.StartWorker();
                }
                catch(Exception ex)
                {
                    Logger.Error(ex);
                }
            }
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == _serverShutdown.Token)
        { } // that's success
    }

    /// <summary>
    /// Listen to a pre-established connection.
    /// </summary>
    public Task ListenAsync(IFrameConnection connection, ILogger? logger = null)
    {
        Logger.Information(connection, static (state, _) => $"accepting connection");
        var server = new LiteConnection(this, connection, logger);
        return server.ExecuteDirect();
    }

    private LiteServiceBinder? _serviceBinder;

    /// <summary>
    /// Gets the <see cref="ServiceBinderBase">binder</see> associated with this server.
    /// </summary>
    public ServiceBinderBase ServiceBinder => _serviceBinder ??= new LiteServiceBinder(this);

    /// <summary>
    /// Gets the number of methods bound to this server, over all services.
    /// </summary>
    public int MethodCount => _methods.Count;

    private readonly ConcurrentDictionary<ReadOnlyMemory<char>, Func<IServerStream>> _methods = new ConcurrentDictionary<ReadOnlyMemory<char>, Func<IServerStream>>(CharMemoryComparer.Instance);

    internal void AddStreamFactory(string fullName, Func<IServerStream> streamFactory)
    {
        if (!_methods.TryAdd(fullName.AsMemory(), streamFactory)) ThrowDuplicate(fullName);
        static void ThrowDuplicate(string fullName) => throw new ArgumentException($"The method '{fullName}' already exists", nameof(fullName));
    }
    internal bool TryCreateStream(ReadOnlyMemory<char> fullName, [MaybeNullWhen(false)] out IServerStream stream)
    {
        stream = _methods.TryGetValue(fullName, out var factory) ? factory?.Invoke() : null;
        return stream is not null;
    }

    private sealed class CharMemoryComparer : IEqualityComparer<ReadOnlyMemory<char>>
    {
        private CharMemoryComparer() { }
        public static CharMemoryComparer Instance { get; } = new CharMemoryComparer();

        bool IEqualityComparer<ReadOnlyMemory<char>>.Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y)
            => x.Span.SequenceEqual(y.Span);

#if NETSTANDARD2_1 || NET472
        int IEqualityComparer<ReadOnlyMemory<char>>.GetHashCode(ReadOnlyMemory<char> obj)
        {
            // pretty random
            if (obj.IsEmpty) return 0;

            var span = obj.Span;
            var hash = (-37 * span.Length) + span[0];
            while (span.Length > 16)
            {
                span = span.Slice(16);
                hash = (-37 * hash) + span[0];
            }
            return hash;
        }
#else
        int IEqualityComparer<ReadOnlyMemory<char>>.GetHashCode(ReadOnlyMemory<char> obj)
            => string.GetHashCode(obj.Span);
#endif
    }
}
