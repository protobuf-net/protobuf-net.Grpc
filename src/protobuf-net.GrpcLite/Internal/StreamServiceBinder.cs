using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

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

    ValueTask PushPayloadAsync(StreamFrame frame, ChannelWriter<StreamFrame> output, ILogger? logger, CancellationToken cancellationToken);
    ValueTask CompleteAsync(CancellationToken cancellationToken);
}

abstract class HandlerBase<TReceive> : IHandler where TReceive : class
{
    private readonly Marshaller<TReceive> _marshaller;
    protected HandlerBase(Marshaller<TReceive> marshaller)
        => _marshaller = marshaller;
    public abstract ValueTask CompleteAsync(CancellationToken cancellationToken);

    Queue<StreamFrame>? _backlog;
    public ValueTask PushPayloadAsync(StreamFrame frame, ChannelWriter<StreamFrame> output, ILogger? logger, CancellationToken cancellationToken)
    {
        if ((frame.KindFlags & (byte)PayloadFlags.Final) == 0)
        {
            (_backlog ??= new Queue<StreamFrame>()).Enqueue(frame);
            return default;
        }
        return PushCompletePayloadAsync(frame, output, logger, cancellationToken);
    }
    private ValueTask PushCompletePayloadAsync(StreamFrame frame, ChannelWriter<StreamFrame> output, ILogger? logger, CancellationToken cancellationToken)
    {
        var backlog = _backlog;
        if (backlog is null || backlog.Count == 0)
        {
            // single frame; simple case
            logger.LogDebug(frame.Length, static (state, _) => $"deserializing request in single buffer, {state} bytes...");
            var ctx = SingleBufferStreamDeserializationContext.Get();
            TReceive request;
            try
            {
                ctx.Initialize(frame.Buffer, frame.Offset, frame.Length);
                request = _marshaller.ContextualDeserializer(ctx);
            }
            finally
            {
                ctx.Recycle();
            }
            logger.LogDebug(frame.Length, static (state, _) => $"deserialized {state} bytes; processing request");
            return PushCompletePayloadAsync(frame.RequestId, output, request, logger, cancellationToken);
        }
        else
        {
            // we have multiple frames to marry
            throw new NotImplementedException();
        }
    }

    protected abstract ValueTask PushCompletePayloadAsync(ushort id, ChannelWriter<StreamFrame> output, TReceive value, ILogger? logger, CancellationToken cancellationToken);
    public abstract FrameKind Kind { get; }
}
class UnaryHandler<TRequest, TResponse> : HandlerBase<TRequest> where TResponse : class where TRequest : class
{
    private readonly UnaryServerMethod<TRequest, TResponse> _handler;
    private readonly Method<TRequest, TResponse> _method;
    public UnaryHandler(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler) : base(method.RequestMarshaller)
    {
        _handler = handler;
        _method = method;
    }

    public override FrameKind Kind => FrameKind.NewUnary;
    public override ValueTask CompleteAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

    protected override async ValueTask PushCompletePayloadAsync(ushort id, ChannelWriter<StreamFrame> output, TRequest value, ILogger? logger, CancellationToken cancellationToken)
    {
        logger.LogDebug(_method, static (state, _) => $"invoking {state.FullName}...");
        TResponse response;
        try
        {
            response = await _handler(value, null!);
            logger.LogDebug(_method, static (state, _) => $"completed {state.FullName}...");
        }
        catch (Exception ex)
        {
            logger.LogError(_method, static (state, ex) => $"faulted {state.FullName}: {ex!.Message}", ex);
            throw;
        }

        var serializationContext = StreamSerializationContext.Get();
        try
        {
            logger.LogDebug(_method, static (state, _) => $"serializing {state.FullName} response...");
            _method.ResponseMarshaller.ContextualSerializer(response, serializationContext);
            logger.LogDebug(serializationContext, static (state, _) => $"serialized {state.Length} bytes");
            var frames = await serializationContext.WritePayloadAsync(output, id, cancellationToken);
            logger.LogDebug(frames, static (state, _) => $"added {state} payload frames");
        }
        finally
        {
            serializationContext.Recycle();
        }
    }
}
