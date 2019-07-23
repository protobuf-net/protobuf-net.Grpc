using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using System.Runtime.CompilerServices;

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TService CreateGrpcService<TService>(this Channel client, ClientFactory? clientFactory = null)
            where TService : class
                    => (clientFactory ?? ClientFactory.Default).CreateClient<TService>(new DefaultCallInvoker(client));
    }
}
