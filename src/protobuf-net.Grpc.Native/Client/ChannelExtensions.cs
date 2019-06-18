using Grpc.Core;
using ProtoBuf.Grpc.Internal;

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
        public static TService CreateGrpcService<TService>(this Channel client)
            where TService : class
        #pragma warning disable CS0618
                    => ClientFactory<ClientBase, TService, Channel>.Create(client);
        #pragma warning restore CS0618
    }
}
