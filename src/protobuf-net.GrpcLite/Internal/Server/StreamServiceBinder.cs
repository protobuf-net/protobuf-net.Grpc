using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal.Server;

internal class StreamServiceBinder : ServiceBinderBase
{
    private readonly StreamServer _server;

    public StreamServiceBinder(StreamServer server) => _server = server;

    public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler)
        => _server.AddHandler(method.FullName, () => new UnaryHandler<TRequest, TResponse>(method, handler));
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
