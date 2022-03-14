using Grpc.Core;

namespace ProtoBuf.Grpc.Lite.Internal.Server;

internal class LiteServiceBinder : ServiceBinderBase
{
    private readonly LiteServer _server;

    public LiteServiceBinder(LiteServer server) => _server = server;

    public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler)
        => _server.AddHandler(method.FullName, () => ServerUnaryHandler<TRequest, TResponse>.Get(method, handler));

    public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ClientStreamingServerMethod<TRequest, TResponse> handler)
    {
        // nothing yet
    }

    public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, DuplexStreamingServerMethod<TRequest, TResponse> handler)
        => _server.AddHandler(method.FullName, () => ServerDuplexHandler<TRequest, TResponse>.Get(method, handler));

    public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ServerStreamingServerMethod<TRequest, TResponse> handler)
    {
        // nothing yet
    }
}
