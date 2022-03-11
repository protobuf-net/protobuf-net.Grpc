using Grpc.Core;

namespace ProtoBuf.Grpc.Lite.Internal.Server;

internal sealed class ServerUnaryHandler<TRequest, TResponse> : ServerHandler<TRequest, TResponse> where TResponse : class where TRequest : class
{
    private UnaryServerMethod<TRequest, TResponse>? _handler;

    public static ServerUnaryHandler<TRequest, TResponse> Get(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler)
    {
        var obj = Pool<ServerUnaryHandler<TRequest, TResponse>>.Get();
        obj.Method = method;
        obj._handler = handler;
        return obj;
    }

    public override void Recycle()
    {
        // not really necessary to reset marshaller/method/handler; they'll be alive globally
        Pool<ServerUnaryHandler<TRequest, TResponse>>.Put(this);
    }

    public override FrameKind Kind => FrameKind.NewUnary;
    public override ValueTask CompleteAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

    private TRequest? _request;

    protected override async Task InvokeServerMethod(ServerCallContext context)
    {
        var response = await _handler!(_request!, context);
        if (context.Status.StatusCode == StatusCode.OK)
        {
            await WritePayloadAsync(response, true);
        }
    }

    protected override ValueTask ReceivePayloadAsync(TRequest value, CancellationToken cancellationToken)
    {
        _request = value;
        Execute();
        return default;
    }
}
