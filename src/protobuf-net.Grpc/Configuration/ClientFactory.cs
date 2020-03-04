using Grpc.Core;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Provides services for creating service clients (proxies)
    /// </summary>
    public abstract class ClientFactory
    {
        /// <summary>
        /// The default client factory (uses the default BinderConfiguration)
        /// </summary>
        public static ClientFactory Default { get; } = DefaultClientFactory.Instance;

        /// <summary>
        /// Create a new client factory; note that non-default factories should be considered expensive, and stored/re-used suitably
        /// </summary>
        public static ClientFactory Create(BinderConfiguration? binderConfiguration = null)
            => (binderConfiguration == null || binderConfiguration == BinderConfiguration.Default) ? Default : new ConfiguredClientFactory(binderConfiguration);

        /// <summary>
        /// Get the binder configuration associated with this instance
        /// </summary>
        protected abstract BinderConfiguration BinderConfiguration { get; }

        /// <summary>
        /// Get the binder configuration associated with this instance
        /// </summary>
        public static implicit operator BinderConfiguration(ClientFactory value) => value.BinderConfiguration;

        /// <summary>
        /// Create a service-client backed by a CallInvoker
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TService CreateClient<TService>(CallInvoker channel) where TService : class
#pragma warning disable CS0618 // SimpleClientBase is marked Obsolete to discourage usage
            => CreateClient<SimpleClientBase, TService, CallInvoker>(channel);
#pragma warning restore CS0618

        /// <summary>
        /// Create a service-client backed by a CallInvoker
        /// </summary>
        public virtual GrpcClient CreateClient(CallInvoker channel, Type contractType)
            => new GrpcClient(channel, contractType, BinderConfiguration);

        // TODO: remove this, standardizing on SimpleClient (or LiteClientBase) and CallInvoker?
        internal abstract TService CreateClient<TBase, TService, TChannel>(TChannel channel) where TService : class;


        private sealed class ConfiguredClientFactory : ClientFactory
        {
            protected override BinderConfiguration BinderConfiguration { get; }

            public ConfiguredClientFactory(BinderConfiguration? binderConfiguration)
            {
                BinderConfiguration = binderConfiguration ?? BinderConfiguration.Default;
            }

            private readonly ConcurrentDictionary<(Type, Type, Type), object> _proxyCache = new ConcurrentDictionary<(Type, Type, Type), object>();

            [MethodImpl(MethodImplOptions.NoInlining)]
            private TService SlowCreateClient<TBase, TService, TChannel>(TChannel channel)
                where TService : class
            {
                var factory = ProxyEmitter.CreateFactory<TChannel, TService>(typeof(TBase), BinderConfiguration);
                var key = (typeof(TBase), typeof(TService), typeof(TChannel));

                if (!_proxyCache.TryAdd(key, factory)) factory = (Func<TChannel, TService>)_proxyCache[key];
                return factory(channel);
            }
            internal override TService CreateClient<TBase, TService, TChannel>(TChannel channel)
                where TService : class
            {
                if (_proxyCache.TryGetValue((typeof(TBase), typeof(TService), typeof(TChannel)), out var obj))
                    return ((Func<TChannel, TService>)obj)(channel);
                return SlowCreateClient<TBase, TService, TChannel>(channel);
            }
        }

        internal static class DefaultProxyCache<TBase, TService, TChannel> where TService : class
        {
            internal static readonly Func<TChannel, TService> Create = ProxyEmitter.CreateFactory<TChannel, TService>(typeof(TBase), BinderConfiguration.Default);
        }

        private sealed class DefaultClientFactory : ClientFactory
        {
            protected override BinderConfiguration BinderConfiguration => BinderConfiguration.Default;

            public static readonly DefaultClientFactory Instance = new DefaultClientFactory();
            private DefaultClientFactory() { }

            internal override TService CreateClient<TBase, TService, TChannel>(TChannel channel) => DefaultProxyCache<TBase, TService, TChannel>.Create(channel);
        }
    }
}
