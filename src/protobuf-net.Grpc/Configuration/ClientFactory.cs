using Grpc.Core;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        public static implicit operator BinderConfiguration(ClientFactory? value) => value?.BinderConfiguration ?? BinderConfiguration.Default;

        /// <summary>
        /// Create a service-client backed by a CallInvoker
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract TService CreateClient<TService>(CallInvoker channel) where TService : class;

        /// <summary>
        /// Create a service-client backed by a CallInvoker
        /// </summary>
        public virtual GrpcClient CreateClient(CallInvoker channel, Type contractType)
            => new GrpcClient(channel, contractType, BinderConfiguration);


        private sealed class ConfiguredClientFactory(BinderConfiguration? binderConfiguration) : ClientFactory
        {
            protected override BinderConfiguration BinderConfiguration { get; } = binderConfiguration ?? BinderConfiguration.Default;

            // values are Func<CallInvoker, TService> once resolved, with an in-progress
            // Lazy<Func<CallInvoker, TService>> placeholder so that concurrent first
            // callers share a single proxy emission (the emitted types are rooted for
            // the lifetime of the module, so redundant emissions are leaked, not just wasted)
            private readonly ConcurrentDictionary<Type, object> _proxyCache = new ConcurrentDictionary<Type, object>();

            [MethodImpl(MethodImplOptions.NoInlining)]
            private TService SlowCreateClient<TService>(CallInvoker channel)
                where TService : class
            {
                var key = typeof(TService);
                var obj = _proxyCache.GetOrAdd(key, _ => new Lazy<Func<CallInvoker, TService>>(
                    () => ProxyEmitter.CreateFactory<TService>(BinderConfiguration, null)));
                if (obj is Lazy<Func<CallInvoker, TService>> pending)
                {
                    Func<CallInvoker, TService> factory;
                    try
                    {
                        factory = pending.Value;
                    }
                    catch
                    {
                        // evict the failed placeholder (if still current), so that
                        // later callers retry rather than observing a cached failure
                        ((ICollection<KeyValuePair<Type, object>>)_proxyCache).Remove(
                            new KeyValuePair<Type, object>(key, obj));
                        throw;
                    }
                    // swap in the resolved delegate for the fast path
                    _proxyCache.TryUpdate(key, factory, obj);
                    return factory(channel);
                }
                return ((Func<CallInvoker, TService>)obj)(channel);
            }
            public override TService CreateClient<TService>(CallInvoker channel)
                where TService : class
            {
                if (_proxyCache.TryGetValue(typeof(TService), out var obj)
                    && obj is Func<CallInvoker, TService> factory)
                {
                    return factory(channel);
                }
                return SlowCreateClient<TService>(channel);
            }
        }

        internal static class DefaultProxyCache<TService> where TService : class
        {
            internal static readonly Func<CallInvoker, TService> Create = ProxyEmitter.CreateFactory<TService>(BinderConfiguration.Default, null);
        }

        private sealed class DefaultClientFactory : ClientFactory
        {
            protected override BinderConfiguration BinderConfiguration => BinderConfiguration.Default;

            public static readonly DefaultClientFactory Instance = new DefaultClientFactory();
            private DefaultClientFactory() { }

            public override TService CreateClient<TService>(CallInvoker channel) => DefaultProxyCache<TService>.Create(channel);
        }
    }
}
