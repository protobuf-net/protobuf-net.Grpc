using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ProtoBuf.Grpc.Configuration;
using Grpc.Core;

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
        /// Create a service-client backed by a CallInvoker
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TService CreateClient<TService>(CallInvoker channel) where TService : class
#pragma warning disable CS0618 // SimpleClientBase is marked Obsolete to discourage usage
            => CreateClient<SimpleClientBase, TService, CallInvoker>(channel);
#pragma warning restore CS0618

        // TODO: remove this, standardizing on SimpleClient (or LiteClientBase) and CallInvoker?
        internal abstract TService CreateClient<TBase, TService, TChannel>(TChannel channel) where TService : class;


        private sealed class ConfiguredClientFactory : ClientFactory
        {
            private readonly BinderConfiguration _binderConfiguration;
            public ConfiguredClientFactory(BinderConfiguration? binderConfiguration)
            {
                _binderConfiguration = binderConfiguration ?? BinderConfiguration.Default;
            }

            private readonly ConcurrentDictionary<(Type, Type, Type), object> _proxyCache = new ConcurrentDictionary<(Type, Type, Type), object>();

            [MethodImpl(MethodImplOptions.NoInlining)]
            private TService SlowCreateClient<TBase, TService, TChannel>(TChannel channel)
                where TService : class
            {
                var factory = ProxyEmitter.CreateFactory<TChannel, TService>(typeof(TBase), _binderConfiguration);
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
            public static readonly DefaultClientFactory Instance = new DefaultClientFactory();
            private DefaultClientFactory() { }

            internal override TService CreateClient<TBase, TService, TChannel>(TChannel channel) => DefaultProxyCache<TBase, TService, TChannel>.Create(channel);
        }
    }
}
