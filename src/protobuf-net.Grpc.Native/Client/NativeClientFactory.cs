using Grpc.Core;
using ProtoBuf.Grpc.Internal;

namespace ProtoBuf.Grpc.Client
{
    public static class ChannelClientFactory
    {
        public static TService Create<TService>(Channel client)
            where TService : class
        #pragma warning disable CS0618
                    => ClientFactory<ClientBase, TService>.Create<Channel>(client);
        #pragma warning restore CS0618
    }
}
