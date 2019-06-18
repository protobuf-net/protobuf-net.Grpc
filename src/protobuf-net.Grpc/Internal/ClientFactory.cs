using System;
using System.ComponentModel;

namespace ProtoBuf.Grpc.Internal
{
    [Obsolete(Reshape.WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public static class ClientFactory<TBase, TService>
        where TService : class
    {
        [Obsolete(Reshape.WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static TService Create<TChannel>(TChannel channel) => ProxyCache<TBase, TChannel, TService>.Create(channel);
    }
    internal static class ProxyCache<TBase, TChannel, TService> where TService : class
    {
        internal static readonly Func<TChannel, TService> Create = ProxyEmitter.CreateFactory<TChannel, TService>(typeof(TBase));
    }
}
