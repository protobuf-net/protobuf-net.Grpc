using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace ProtoBuf.Grpc.Lite.Internal;

internal class StreamServiceBinder : ServiceBinderBase
{
    private readonly StreamServer _server;

    public StreamServiceBinder(StreamServer server) => _server = server;

    public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler)
        => _server.AddHandler(method.FullName, () => new UnaryHandler<TRequest, TResponse>(method, handler));
}

interface IHandler
{
    FrameKind Kind { get; }

    ValueTask PushPayloadAsync(StreamFrame frame, ILogger? logger, CancellationToken cancellationToken);
    ValueTask CompleteAsync(CancellationToken cancellationToken);
}

abstract class HandlerBase<TRequest, TResponse> : IHandler where TResponse : class where TRequest : class
{
    private readonly Method<TRequest, TResponse> _method;
    protected HandlerBase(Method<TRequest, TResponse> method)
        => _method = method;
    public abstract ValueTask CompleteAsync(CancellationToken cancellationToken);

    Queue<StreamFrame>? _backlog;
    public ValueTask PushPayloadAsync(StreamFrame frame, ILogger? logger, CancellationToken cancellationToken)
    {
        if ((frame.KindFlags & (byte)PayloadFlags.Final) == 0)
        {
            (_backlog ??= new Queue<StreamFrame>()).Enqueue(frame);
            return default;
        }
        return PushCompletePayloadAsync(frame, logger, cancellationToken);
    }
    private ValueTask PushCompletePayloadAsync(StreamFrame frame, ILogger? logger, CancellationToken cancellationToken)
    {
        var backlog = _backlog;
        if (backlog is null || backlog.Count == 0)
        {
            // single frame; simple case
            logger.LogDebug(frame.Length, static (state, _) => $"deserializing request in single buffer, {state} bytes...");
            var ctx = SingleBufferStreamDeserializationContext.Get();
            ctx.Initialize(frame.Buffer, frame.Offset, frame.Length);
            var value = _method.RequestMarshaller.ContextualDeserializer(ctx);
            ctx.Recycle();
            logger.LogDebug(frame.Length, static (state, _) => $"deserialized {state} bytes; processing request");
            return PushCompletePayloadAsync(value, logger, cancellationToken);
        }
        else
        {
            // we have multiple frames to marry
            throw new NotImplementedException();
        }
    }

    protected abstract ValueTask PushCompletePayloadAsync(TRequest value, ILogger? logger, CancellationToken cancellationToken);
    public abstract FrameKind Kind { get; }
}
class UnaryHandler<TRequest, TResponse> : HandlerBase<TRequest, TResponse> where TResponse : class where TRequest : class
{
    private readonly UnaryServerMethod<TRequest, TResponse> _handler;
    private readonly Method<TRequest, TResponse> _method;
    public UnaryHandler(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler) : base(method)
    {
        _handler = handler;
        _method = method;
    }

    public override FrameKind Kind => FrameKind.NewUnary;
    public override ValueTask CompleteAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

    protected override async ValueTask PushCompletePayloadAsync(TRequest value, ILogger? logger, CancellationToken cancellationToken)
    {
        logger.LogDebug(_method, static (state, _) => $"invoking {state.FullName}...");
        try
        {
            var response = await _handler(value, null!);
            logger.LogDebug(_method, static (state, _) => $"completed {state.FullName}...");
        }
        catch (Exception ex)
        {
            logger.LogDebug(_method, static (state, ex) => $"faulted {state.FullName}: {ex!.Message}", ex);
        }
    }
}
