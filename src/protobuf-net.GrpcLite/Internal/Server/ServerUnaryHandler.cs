using Grpc.Core;
using ProtoBuf.Grpc.Lite.Connections;

namespace ProtoBuf.Grpc.Lite.Internal.Server;

internal sealed class ServerUnaryHandler<TRequest, TResponse> : ServerHandler<TRequest, TResponse> where TResponse : class where TRequest : class
{
    private UnaryServerMethod<TRequest, TResponse> _handler = null!;

    public static ServerUnaryHandler<TRequest, TResponse> Get(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler)
    {
        var obj = Pool<ServerUnaryHandler<TRequest, TResponse>>.Get();
        obj.Method = method;
        obj._handler = handler;
        return obj;
    }

    public override void Recycle()
    {
        Method = null!;
        _handler = null!;
        Pool<ServerUnaryHandler<TRequest, TResponse>>.Put(this);
    }

    private bool haveRequest = false;
    private TRequest? _request;

    protected override async Task InvokeServerMethod(ServerCallContext context)
    {
        TRequest value;
        lock (this)
        {
            if (!haveRequest) ThrowNoRequest();
            value = _request!;
        }
        var response = await _handler(value, context);
        if (context.Status.StatusCode == StatusCode.OK)
        {
            await SendAsync(response, PayloadFlags.FinalItem, context.CancellationToken);
        }

        static void ThrowNoRequest() => throw new InvalidOperationException("No request was received");
    }

    protected override ValueTask OnPayloadAsync(TRequest value)
    {
        lock (this)
        {
            if (haveRequest) ThrowMultipleRequests();
            _request = value;
            haveRequest = true;
        }
        return InvokeAndCompleteAsync();

        static void ThrowMultipleRequests() => throw new InvalidOperationException("Additional request payloads are not expected");
    }
}
