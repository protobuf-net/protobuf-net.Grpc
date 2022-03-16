using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ProtoBuf.Grpc.Lite.Internal.Client;

internal sealed class LiteCallInvoker : CallInvoker, IListener, IWorker
{
    private readonly ILogger? _logger;
    private readonly IFrameConnection _connection;
    private readonly string _target;
    private readonly CancellationTokenSource _clientShutdown = new();
    private readonly ConcurrentDictionary<ushort, IStream> _streams = new();

    public override string ToString() => _target;

    private int _nextId = ushort.MaxValue; // so that our first id is zero
    private void AddStream(IClientHandler handler, IMethod method)
    {
        for (int i = 0; i < 1024; i++) // try *reasonably* hard to get a new stream id, without going mad
        {
            var id = Utilities.IncrementToUInt32(ref _nextId);
            handler.Id = id;
            if (_streams.TryAdd(id, handler))
            {
                handler.Initialize(method, _connection, _logger);
                return;
            }
        }
        ThrowUnableToReserve(_streams.Count);
        static void ThrowUnableToReserve(int count)
            => throw new InvalidOperationException($"It was not possible to reserve a new stream id; {count} streams are currently in use");
    }

    internal void StopWorker() => _clientShutdown.Cancel();

    public LiteCallInvoker(string target, IFrameConnection connection, ILogger? logger)
    {
        this._target = target;
        this._connection = connection;
        this._logger = logger;
    }

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        => SimpleUnary(method, host, options, request).GetAwaiter().GetResult();

    async Task<TResponse> SimpleUnary<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        where TRequest : class
        where TResponse : class
    {
        var handler = ClientUnaryHandler<TRequest, TResponse>.Get(options.CancellationToken);
        AddStream(handler, method);
        try
        {
            await handler.SendSingleAsync(host, options, request);
            return await handler.ResponseAsync;
        }
        finally
        {   // since we've never exposed this to the external world, we can safely recycle it
            handler.Recycle();
        }
    }

    static readonly Func<object, Task<Metadata>> s_responseHeadersAsync = static state => ((IClientHandler)state).ResponseHeadersAsync;
    static readonly Func<object, Status> s_getStatus = static state => ((IClientHandler)state).Status;
    static readonly Func<object, Metadata> s_getTrailers = static state => ((IClientHandler)state).Trailers();
    static readonly Action<object> s_dispose = static state => ((IClientHandler)state).Dispose();

    bool IListener.IsClient => true;

    IFrameConnection IListener.Connection => _connection;

    ConcurrentDictionary<ushort, IStream> IListener.Streams => _streams;

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        var handler = ClientUnaryHandler<TRequest, TResponse>.Get(options.CancellationToken);
        AddStream(handler, method);
        _ = handler.SendSingleAsync(host, options, request);
        return new AsyncUnaryCall<TResponse>(handler.ResponseAsync, s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, handler);
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
    {
        var handler = ClientUnaryHandler<TRequest, TResponse>.Get(options.CancellationToken);
        AddStream(handler, method);
        _ = handler.SendInitializeAsync(host, options).AsTask();
        return new AsyncClientStreamingCall<TRequest, TResponse>(handler, handler.ResponseAsync, s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, handler);
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
    {
        var handler = ClientServerStreamingHandler<TRequest, TResponse>.Get();
        AddStream(handler, method);
        _ = handler.SendInitializeAsync(host, options).AsTask();
        return new AsyncDuplexStreamingCall<TRequest, TResponse>(handler, handler, s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, handler);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        var handler = ClientServerStreamingHandler<TRequest, TResponse>.Get();
        AddStream(handler, method);
        _ = handler.SendSingleAsync(host, options, request);
        return new AsyncServerStreamingCall<TResponse>(handler, s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, handler);
    }

    bool IListener.TryCreateStream(in Frame initialize, [MaybeNullWhen(false)] out IStream handler)
    {
        // not accepting server-initialized streams
        handler = null;
        return false;
    }

    public void Execute()
    {
        Logging.SetSource(Logging.ClientPrefix + "invoker");
        _logger.Debug(_target, (state, _) => $"Starting call-invoker (client): {state}...");
        _ = this.RunAsync(_logger, _clientShutdown.Token);
    }
}
