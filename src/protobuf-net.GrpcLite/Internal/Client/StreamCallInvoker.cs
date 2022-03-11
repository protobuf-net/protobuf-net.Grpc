using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal.Client;

internal sealed class StreamCallInvoker : CallInvoker
{
    private readonly ILogger? _logger;
    private readonly Channel<StreamFrame> _outbound;
    private readonly ConcurrentDictionary<ushort, IClientHandler> _activeOperations = new();
    private int _nextId = -1; // so that our first id is zero
    private ushort NextId()
    {
        while (true)
        {
            var id = Interlocked.Increment(ref _nextId);
            if (id <= ushort.MaxValue && id >= 0) return (ushort)id; // in-range; that'll do

            // try to swap to zero; if we win: we are become zero
            if (Interlocked.CompareExchange(ref _nextId, 0, id) == id) return 0;

            // otherwise, redo from start
        }
    }

    public StreamCallInvoker(Channel<StreamFrame> outbound, ILogger? logger)
    {
        this._outbound = outbound;
    }

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        => SimpleUnary(method, host, options, request).GetAwaiter().GetResult();

    async Task<TResponse> SimpleUnary<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        where TRequest : class
        where TResponse : class
    {
        var handler = ClientUnaryHandler<TRequest, TResponse>.Get(options.CancellationToken);
        handler.Initialize(NextId(), method, _outbound, _logger);
        AddHandler(handler);
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
        handler.Initialize(NextId(), method, _outbound, _logger);
        AddHandler(handler);
        _ = handler.SendSingleAsync(host, options, request);
        return new AsyncUnaryCall<TResponse>(handler.ResponseAsync, s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, handler);
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
    {
        var handler = ClientUnaryHandler<TRequest, TResponse>.Get(options.CancellationToken);
        handler.Initialize(NextId(), method, _outbound, _logger);
        AddHandler(handler);
        _ = handler.SendInitializeAsync(host, options).AsTask();
        return new AsyncClientStreamingCall<TRequest, TResponse>(handler, handler.ResponseAsync, s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, handler);
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
    {
        var handler = ClientServerStreamingHandler<TRequest, TResponse>.Get(FrameKind.NewDuplex);
        handler.Initialize(NextId(), method, _outbound, _logger);
        AddHandler(handler);
        _ = handler.SendInitializeAsync(host, options).AsTask();
        return new AsyncDuplexStreamingCall<TRequest, TResponse>(handler, handler, s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, handler);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        var handler = ClientServerStreamingHandler<TRequest, TResponse>.Get(FrameKind.NewServerStreaming);
        handler.Initialize(NextId(), method, _outbound, _logger);
        AddHandler(handler);
        _ = handler.SendSingleAsync(host, options, request);
        return new AsyncServerStreamingCall<TResponse>(handler, s_responseHeadersAsync, s_getStatus, s_getTrailers, s_dispose, handler);
    }

    internal async Task ConsumeAsync(Stream input, ILogger? logger, CancellationToken cancellationToken)
    {
        await Task.Yield();
        while (!cancellationToken.IsCancellationRequested)
        {
            var frame = await StreamFrame.ReadAsync(input, cancellationToken);
            logger.LogDebug(frame, static (state, _) => $"received frame {state}");
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
                    logger.LogError(frame, static (state, _) => $"server should not be initializing requests! {state}");
                    break;
                case FrameKind.Payload:
                    if (_activeOperations.TryGetValue(frame.RequestId, out var handler))
                    {
                        await handler.ReceivePayloadAsync(frame, cancellationToken);
                    }
                    break;
            }
        }
    }

    private void AddHandler(IClientHandler receiver)
    {
        if (receiver is null) ThrowNull();
        if (!_activeOperations.TryAdd(receiver!.Id, receiver)) ThrowDuplicate(receiver.Id);

        static void ThrowNull() => throw new ArgumentNullException(nameof(receiver));
        static void ThrowDuplicate(ushort id) => throw new ArgumentException($"Duplicate receiver key: {id}");
    }
}
