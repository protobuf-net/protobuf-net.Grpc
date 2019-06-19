using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Internal;
using System;
using System.Net.Http;
using ProtoBuf.Grpc.Configuration;

namespace ProtoBuf.Grpc.Client
{
    /// <summary>
    /// Provides extension methods to the HttpClient API
    /// </summary>
    public static class HttpClientExtensions
    {
        private const string Switch_AllowUnencryptedHttp2 = "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport";
        /// <summary>
        /// Allows HttpClient to use HTTP/2 without TLS
        /// </summary>
        public static bool AllowUnencryptedHttp2
        {
            get => AppContext.TryGetSwitch(Switch_AllowUnencryptedHttp2, out var enabled) ? enabled : false;
            set => AppContext.SetSwitch(Switch_AllowUnencryptedHttp2, true);
        }

        /// <summary>
        /// Creates a code-first service backed by an HttpClient instance
        /// </summary>
        public static TService CreateGrpcService<TService>(this HttpClient client, ILoggerFactory? logger = null, ClientFactory? clientFactory = null)
            where TService : class
#pragma warning disable CS0618
            => (clientFactory ?? ClientFactory.Default).CreateClient<SimpleClientBase, TService, CallInvoker>(new HttpClientCallInvoker(client, logger));
#pragma warning restore CS0618
    }
}
