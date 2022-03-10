using Grpc.Core;

namespace ProtoBuf.Grpc.Lite.Internal;

internal class StreamServiceBinder : ServiceBinderBase
{
    private readonly StreamServer _server;

    public StreamServiceBinder(StreamServer server) => _server = server;

    public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler)
        => _server.Add(method.FullName, () => new UnaryHandler<TRequest, TResponse>(method, handler));
}

interface IHandler
{
    ValueTask PushAsync(StreamFrame frame);
    ValueTask Complete();
}

abstract class HandlerBase<TRequest, TResponse> : IHandler where TResponse : class where TRequest : class
{
    private readonly Method<TRequest, TResponse> _method;
    protected HandlerBase(Method<TRequest, TResponse> method)
        => _method = method;
    public abstract ValueTask Complete();
    public abstract ValueTask PushAsync(StreamFrame frame);
}
class UnaryHandler<TRequest, TResponse> : HandlerBase<TRequest, TResponse> where TResponse : class where TRequest : class
{
    private readonly UnaryServerMethod<TRequest, TResponse> _handler;
    public UnaryHandler(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler) : base(method) 
        => _handler = handler;

    public override ValueTask Complete() => throw new NotImplementedException();
    public override ValueTask PushAsync(StreamFrame frame) => throw new NotImplementedException();
}
