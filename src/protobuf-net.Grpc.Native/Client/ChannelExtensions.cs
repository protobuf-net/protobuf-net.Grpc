using Grpc.Core;
using ProtoBuf.Grpc.Internal;

namespace ProtoBuf.Grpc.Client
{
    public static class ChannelExtensions
    {
        public static TService CreateGrpcService<TService>(this Channel client)
            where TService : class
        #pragma warning disable CS0618
                    => ClientFactory<ClientBase, TService, Channel>.Create(client);
        #pragma warning restore CS0618
    }
}
