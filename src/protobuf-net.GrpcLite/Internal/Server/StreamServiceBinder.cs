using Grpc.Core;

namespace ProtoBuf.Grpc.Lite.Internal.Server;

internal class StreamServiceBinder : ServiceBinderBase
{
    private readonly StreamServer _server;

    public StreamServiceBinder(StreamServer server) => _server = server;

    public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler)
        => _server.AddHandler(method.FullName, () => ServerUnaryHandler<TRequest, TResponse>.Get(method, handler));
}
