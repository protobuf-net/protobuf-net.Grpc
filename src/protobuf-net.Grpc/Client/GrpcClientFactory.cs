using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Client
{
    /// <summary>
    /// Provides extension methods to the native Channel API
    /// </summary>
    public static class GrpcClientFactory
    {
#if NET5_0_OR_GREATER && !NET8_0_OR_GREATER
        // For older trimmer scenarios, hint the linker to keep ClientBase members so the IL-emit fallback
        // can derive from it. From .NET 8+ this annotation cascades trim warnings (because some kept
        // members are themselves [RequiresUnreferencedCode]), and the IL-emit path is now gated on
        // RuntimeFeature.IsDynamicCodeSupported anyway — so drop the annotation for net8+ AOT.
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
        internal static readonly Type ClientBaseType = typeof(ClientBase);

        private const string SwitchAllowUnencryptedHttp2 = "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport";
        /// <summary>
        /// Allows HttpClient to use HTTP/2 without TLS
        /// </summary>
        public static bool AllowUnencryptedHttp2
        {
            get => AppContext.TryGetSwitch(SwitchAllowUnencryptedHttp2, out var enabled) && enabled;
            set => AppContext.SetSwitch(SwitchAllowUnencryptedHttp2, value);
        }

        /// <summary>
        /// Creates a code-first service backed by a ChannelBase instance
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TService CreateGrpcService<TService>(this ChannelBase client, ClientFactory? clientFactory = null)
            where TService : class
                    => CreateGrpcService<TService>(client.CreateCallInvoker(), clientFactory);

        /// <summary>
        /// Creates a code-first service backed by a ChannelBase instance
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TService CreateGrpcService<TService>(this CallInvoker client, ClientFactory? clientFactory = null)
            where TService : class
                    => (clientFactory ?? ClientFactory.Default).CreateClient<TService>(client);

        /// <summary>
        /// Creates a general purpose service backed by a ChannelBase instance
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GrpcClient CreateGrpcService(this ChannelBase client, Type serviceType, ClientFactory? clientFactory = null)
            => CreateGrpcService(client.CreateCallInvoker(), serviceType, clientFactory);

        /// <summary>
        /// Creates a general purpose service backed by a ChannelBase instance
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GrpcClient CreateGrpcService(this CallInvoker client, Type serviceType, ClientFactory? clientFactory = null)
            => (clientFactory ?? ClientFactory.Default).CreateClient(client, serviceType);
    }
}
