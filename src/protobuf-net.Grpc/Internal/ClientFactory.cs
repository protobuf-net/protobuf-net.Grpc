using System;

namespace ProtoBuf.Grpc.Internal
{
    internal static class ClientFactory<TBase, TService, TChannel>
        where TService : class
    {
        public static TService Create(TChannel channel) => ProxyCache<TBase, TService, TChannel>.Create(channel);
    }
    internal static class ProxyCache<TBase, TService, TChannel> where TService : class
    {
        internal static readonly Func<TChannel, TService> Create = ProxyEmitter.CreateFactory<TChannel, TService>(typeof(TBase));
    }
}
