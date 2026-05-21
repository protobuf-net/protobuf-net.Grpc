using Grpc.Core;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace ProtoBuf.Grpc.Internal
{
    /// <summary>
    /// <para>
    /// Holds the build-time generated client factories and server bindings emitted by
    /// <c>protobuf-net.Grpc.BuildTools</c>. The generator emits a <c>[ModuleInitializer]</c> per
    /// service contract that populates this registry, so consumption is fully static — no
    /// reflection over the contract interface at runtime.
    /// </para>
    /// <para>
    /// This is intended for use by the source generator only; the API is exposed publicly so the
    /// generated module initializer can call it from any assembly.
    /// </para>
    /// </summary>
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public static class GeneratedProxyRegistry
    {
        private static readonly ConcurrentDictionary<Type, Delegate> s_clientFactories = new();
        private static readonly ConcurrentDictionary<Type, Type> s_serverBindings = new();

        /// <summary>
        /// Register a generated client proxy factory for <typeparamref name="TService"/>. Called from
        /// the generated <c>[ModuleInitializer]</c>; do not call directly.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterClient<TService>(Func<CallInvoker, TService> factory)
            where TService : class
        {
            if (factory is null) throw new ArgumentNullException(nameof(factory));
            s_clientFactories.TryAdd(typeof(TService), factory);
        }

        /// <summary>
        /// Register a generated server bindings type for <paramref name="contractType"/>. The type
        /// must expose a <c>public static int Bind&lt;TService&gt;(IServerMethodBinder&lt;TService&gt; binder)</c>
        /// method (the generator emits this). Called from the generated <c>[ModuleInitializer]</c>;
        /// do not call directly.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void RegisterServer(Type contractType,
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
#endif
            Type generatedBindingsType)
        {
            if (contractType is null) throw new ArgumentNullException(nameof(contractType));
            if (generatedBindingsType is null) throw new ArgumentNullException(nameof(generatedBindingsType));
            s_serverBindings.TryAdd(contractType, generatedBindingsType);
        }

        /// <summary>
        /// Try to resolve a generated client factory for <typeparamref name="TService"/>.
        /// </summary>
        internal static Func<CallInvoker, TService>? TryGetClientFactory<TService>()
            where TService : class
            => s_clientFactories.TryGetValue(typeof(TService), out var d)
                ? (Func<CallInvoker, TService>)d
                : null;

        /// <summary>
        /// Try to resolve a generated server bindings type for <paramref name="contractType"/>.
        /// </summary>
        internal static bool TryGetServerBindings(Type contractType,
#if NET8_0_OR_GREATER
            [NotNullWhen(true)][DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
#endif
            out Type? generatedBindingsType)
            => s_serverBindings.TryGetValue(contractType, out generatedBindingsType);
    }
}
