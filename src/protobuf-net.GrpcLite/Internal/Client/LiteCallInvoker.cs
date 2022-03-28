using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite.Internal.Client;

internal sealed class LiteCallInvoker : CallInvoker, IConnection, IWorker
{
    private readonly ILogger? _logger;
    private readonly IFrameConnection _connection;
    private readonly ChannelWriter<(Frame Frame, FrameWriteFlags Flags)> _output;
    private readonly string _target;
    private readonly CancellationTokenSource _clientShutdown = new();
    private readonly ConcurrentDictionary<ushort, IStream> _streams = new();

    RefCountedMemoryPool<byte> IConnection.Pool => RefCountedMemoryPool<byte>.Shared;

    public override string ToString() => _target;

    string IConnection.LastKnownUserAgent
    {   // not expecting this to get used on client connections
        get => "";
        set { } 
    }

    private int _nextId = ushort.MaxValue; // so that our first id is zero

    void IConnection.Remove(ushort id) => _streams.TryRemove(id, out _);

    CancellationToken IConnection.Shutdown => _clientShutdown.Token;

    private ClientStream<TRequest, TResponse> AddClientStream<TRequest, TResponse>(Method<TRequest, TResponse> method, in CallOptions options)
        where TRequest : class where TResponse : class
    {
        var stream = new ClientStream<TRequest, TResponse>(method, this, _logger);
        for (int i = 0; i < 1024; i++) // try *reasonably* hard to get a new stream id, without going mad
        {
            // MSB bit is always off for clients (and call-invoker is always a client)
            var id = (ushort)(Utilities.IncrementToUInt32(ref _nextId) & 0x7FFF);
            stream.Id = id;
            if (_streams.TryAdd(id, stream))
            {
                stream.RegisterForCancellation(options.CancellationToken, options.Deadline);
                return stream;
            }
        }
        return ThrowUnableToReserve(_streams.Count);
        static ClientStream<TRequest, TResponse> ThrowUnableToReserve(int count)
            => throw new InvalidOperationException($"It was not possible to reserve a new stream id; {count} streams are currently in use");
    }

    void IConnection.Close(Exception? fault) => StopWorker();
    internal void StopWorker()
    {
        try
        {
            if (!_clientShutdown.IsCancellationRequested)
            {
                _clientShutdown.Cancel();
            }
        }
        catch { }
        _clientShutdown.SafeDispose();
    }

    public LiteCallInvoker(string target, IFrameConnection connection, ILogger? logger)
    {
        this._target = target;
        this._connection = connection;
        this._logger = logger;
        _ = connection.StartWriterAsync(this, out _output, _clientShutdown.Token);
    }

    ChannelWriter<(Frame Frame, FrameWriteFlags Flags)> IConnection.Output => _output;

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        => SimpleUnary(method, host, options, request).GetAwaiter().GetResult();

    async Task<TResponse> SimpleUnary<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        where TRequest : class
        where TResponse : class
    {
        using var stream = AddClientStream(method, options);
        await stream.SendSingleAsync(host, options, request);
        return await stream.AssertSingleAsync();
    }

    static readonly Func<object, Task<Metadata>> s_responseHeadersAsync = static state => ((IClientStream)state).ResponseHeadersAsync;
    static readonly Func<object, Status> s_getStatus = static state => ((IClientStream)state).Status;
    static readonly Func<object, Metadata> s_getTrailers = static state => ((IClientStream)state).Trailers();
    static readonly Action<object> s_dispose = static state => ((IClientStream)state).Dispose();

    bool IConnection.IsClient => true;

    IAsyncEnumerable<Frame> IConnection.Input => _connection;

    ConcurrentDictionary<ushort, IStream> IConnection.Streams => _streams;

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        var stream = AddClientStream(method, options);
        _ = stream.SendSingleAsync(host, options, request);
        return new AsyncUnaryCall<TResponse>(stream.AssertSingleAsync().AsTask(),
            s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, stream);
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
    {
        var stream = AddClientStream(method, options);
        _ = stream.SendHeaderAsync(host, options, FrameWriteFlags.None).AsTask();
        return new AsyncClientStreamingCall<TRequest, TResponse>(stream, stream.AssertSingleAsync().AsTask(),
            s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, stream);
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
    {
        var stream = AddClientStream(method, options);
        _ = stream.SendHeaderAsync(host, options, FrameWriteFlags.None).AsTask();
        return new AsyncDuplexStreamingCall<TRequest, TResponse>(stream, stream, s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, stream);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        var stream = AddClientStream(method, options);
        _ = stream.SendSingleAsync(host, options, request);
        return new AsyncServerStreamingCall<TResponse>(stream, s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, stream);
    }

    bool IConnection.TryCreateStream(in Frame initialize, ReadOnlyMemory<char> route, [MaybeNullWhen(false)] out IStream stream)
    {
        // not accepting server-initialized streams
        stream = null;
        return false;
    }

    public void Execute()
    {
        _logger.SetSource(LogKind.Client, "invoker");
        _logger.Debug(_target, static (state, _) => $"Starting call-invoker (client): {state}...");
        _ = this.RunAsync(_logger, _clientShutdown.Token);
    }
}
