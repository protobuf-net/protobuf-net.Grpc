﻿using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Collections.Concurrent;

namespace ProtoBuf.Grpc.Lite.Internal.Client;

internal sealed class LiteCallInvoker : CallInvoker
{
    private readonly ILogger? _logger;
    private readonly IFrameConnection _connection;
    private readonly ConcurrentDictionary<ushort, IClientHandler> _streams = new();
    private int _nextId = ushort.MaxValue; // so that our first id is zero
    private ushort AddStream(IClientHandler handler)
    {
        for (int i = 0; i < 1024; i++) // try *reasonably* hard to get a new stream id, without going mad
        {
            var id = Utilities.IncrementToUInt32(ref _nextId);
            handler.Id = id;
            if (_streams.TryAdd(id, handler)) return id;
        }
        return ThrowUnableToReserve(_streams.Count);
        static ushort ThrowUnableToReserve(int count)
            => throw new InvalidOperationException($"It was not possible to reserve a new stream id; {count} streams are currently in use");
    }

    public LiteCallInvoker(IFrameConnection connection, ILogger? logger)
    {
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
        handler.Initialize(AddStream(handler), method, _connection, _logger);
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

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        var handler = ClientUnaryHandler<TRequest, TResponse>.Get(options.CancellationToken);
        handler.Initialize(AddStream(handler), method, _connection, _logger);
        _ = handler.SendSingleAsync(host, options, request);
        return new AsyncUnaryCall<TResponse>(handler.ResponseAsync, s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, handler);
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
    {
        var handler = ClientUnaryHandler<TRequest, TResponse>.Get(options.CancellationToken);
        handler.Initialize(AddStream(), method, _connection, _logger);
        AddHandler(handler);
        _ = handler.SendInitializeAsync(host, options).AsTask();
        return new AsyncClientStreamingCall<TRequest, TResponse>(handler, handler.ResponseAsync, s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, handler);
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
    {
        var handler = ClientServerStreamingHandler<TRequest, TResponse>.Get(FrameKind.DuplexStreaming);
        handler.Initialize(AddStream(handler), method, _connection, _logger);
        _ = handler.SendInitializeAsync(host, options).AsTask();
        return new AsyncDuplexStreamingCall<TRequest, TResponse>(handler, handler, s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, handler);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        var handler = ClientServerStreamingHandler<TRequest, TResponse>.Get(FrameKind.ServerStreaming);
        handler.Initialize(AddStream(handler), method, _connection, _logger);
        _ = handler.SendSingleAsync(host, options, request);
        return new AsyncServerStreamingCall<TResponse>(handler, s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, handler);
    }

    internal async Task ReadAllAsync(ILogger? logger, CancellationToken cancellationToken)
    {
        await Task.Yield();
        await using var iter = _connection.GetAsyncEnumerator(cancellationToken);
        while (!cancellationToken.IsCancellationRequested && await iter.MoveNextAsync())
        {
            var (header, payload) = iter.Current;
            logger.LogDebug(header, static (state, _) => $"received frame {state}");
            switch (header.Kind)
            {
                case FrameKind.Close:
                case FrameKind.Ping:
                    var generalFlags = (GeneralFlags)header.KindFlags;
                    if ((generalFlags & GeneralFlags.IsResponse) == 0)
                    {
                        // if this was a request, we reply in kind, but noting that it is a response
                        await _outbound.Writer.WriteAsync(new Frame(frame.Kind, frame.RequestId, (byte)GeneralFlags.IsResponse), cancellationToken);
                    }
                    // shutdown if requested
                    if (header.Kind == FrameKind.Close)
                    {
                        _outbound.Writer.TryComplete();
                    }
                    break;
                case FrameKind.Unary:
                case FrameKind.ClientStreaming:
                case FrameKind.ServerStreaming:
                case FrameKind.DuplexStreaming:
                    logger.LogError(header, static (state, _) => $"server should not be initializing requests! {state}");
                    break;
                case FrameKind.Payload:
                    if (_streams.TryGetValue(header.StreamId, out var handler))
                    {
                        await handler.ReceivePayloadAsync(payload, cancellationToken);
                    }
                    break;
            }
        }
    }
}
