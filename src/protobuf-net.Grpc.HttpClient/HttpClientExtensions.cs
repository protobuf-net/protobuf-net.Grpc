using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Internal;
using System;
using System.Net.Http;

namespace ProtoBuf.Grpc.Client
{
    public static class HttpClientExtensions
    {
        private const string Switch_AllowUnencryptedHttp2 = "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport";
        public static bool AllowUnencryptedHttp2
        {
            get => AppContext.TryGetSwitch(Switch_AllowUnencryptedHttp2, out var enabled) ? enabled : false;
            set => AppContext.SetSwitch(Switch_AllowUnencryptedHttp2, true);
        }

        public static TService CreateGrpcService<TService>(this HttpClient client, ILoggerFactory? logger = null)
            where TService : class
#pragma warning disable CS0618
            => ClientFactory<SimpleClientBase, TService, CallInvoker>.Create(new HttpClientCallInvoker(client, logger));
#pragma warning restore CS0618
    }
}
