using Grpc.Core;

namespace ProtoBuf.Grpc.Lite.Internal.Server;

internal class LiteServiceBinder : ServiceBinderBase
{
    private readonly LiteServer _server;

    public LiteServiceBinder(LiteServer server) => _server = server;

    private void Add<TRequest, TResponse>(Method<TRequest, TResponse> method, object executor)
        where TRequest : class where TResponse : class
        => _server.AddStreamFactory(method.FullName, () => new ServerStream<TRequest, TResponse>(method, executor));
    public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler)
        => Add(method, handler);

    public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ClientStreamingServerMethod<TRequest, TResponse> handler)
        => Add(method, handler);

    public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, DuplexStreamingServerMethod<TRequest, TResponse> handler)
        => Add(method, handler);

    public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ServerStreamingServerMethod<TRequest, TResponse> handler)
        => Add(method, handler);
}
