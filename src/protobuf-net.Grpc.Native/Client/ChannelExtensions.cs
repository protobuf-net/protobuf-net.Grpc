using Grpc.Core;
using ProtoBuf.Grpc.Configuration;

namespace ProtoBuf.Grpc.Client
{
    /// <summary>
    /// Provides extension methods to the native Channel API
    /// </summary>
    public static class ChannelExtensions
    {
        /// <summary>
        /// Creates a code-first service backed by a Channel instance
        /// </summary>
        public static TService CreateGrpcService<TService>(this Channel client, ClientFactory? clientFactory = null)
            where TService : class
                    => (clientFactory ?? ClientFactory.Default).CreateClient<ClientBase, TService, Channel>(client);
    }
}
