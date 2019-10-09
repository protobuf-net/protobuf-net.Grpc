using Grpc.Core;
using ProtoBuf.Grpc.Server;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using Xunit;

namespace protobuf_net.Grpc.Test.Integration
{
    [DataContract]
    public class Apply
    {
        public Apply() { }
        public Apply(int x, int y) => (X, Y) = (x, y);

        [DataMember(Order = 1)]
        public int X { get; set; }

        [DataMember(Order = 2)]
        public int Y { get; set; }
    }

    [DataContract]
    public class ApplyResponse
    {
        public ApplyResponse() { }
        public ApplyResponse(int result) => Result = result;

        [DataMember(Order = 1)]
        public int Result { get; set; }
    }
    
    public class ApplyServices : IGrpcService
    {
        public Task<ApplyResponse> Add(Apply request) => Task.FromResult(new ApplyResponse(request.X + request.Y));
        public Task<ApplyResponse> Mul(Apply request) => Task.FromResult(new ApplyResponse(request.X * request.Y));
        public Task<ApplyResponse> Sub(Apply request) => Task.FromResult(new ApplyResponse(request.X - request.Y));
        public Task<ApplyResponse> Div(Apply request) => Task.FromResult(new ApplyResponse(request.X / request.Y));
    }

    public class GrpcServiceFixture : IAsyncDisposable
    {
        public const int Port = 10042;
        private readonly Server _server;
        
        public GrpcServiceFixture()
        {
            _server = new Server
            {
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            int opCount = _server.Services.AddCodeFirst(new ApplyServices());
            _server.Start();
        }

        public async ValueTask DisposeAsync()
        {
            await _server.ShutdownAsync();
        }
    }
    
    public class GrpcServiceTests : IClassFixture<GrpcServiceFixture>
    {
        private GrpcServiceFixture _fixture;
        public GrpcServiceTests(GrpcServiceFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task CanCallAllApplyServices()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{GrpcServiceFixture.Port}");

            var request = new Apply { X = 6, Y = 3 };
            var invoker = http.CreateCallInvoker();

            var response = await invoker.Execute<Apply, ApplyResponse>(request, nameof(ApplyServices), nameof(ApplyServices.Add));
            Assert.Equal(9, response.Result);
            response = await invoker.Execute<Apply, ApplyResponse>(request, nameof(ApplyServices), nameof(ApplyServices.Mul));
            Assert.Equal(18, response.Result);
            response = await invoker.Execute<Apply, ApplyResponse>(request, nameof(ApplyServices), nameof(ApplyServices.Sub));
            Assert.Equal(3, response.Result);
            response = await invoker.Execute<Apply, ApplyResponse>(request, nameof(ApplyServices), nameof(ApplyServices.Div));
            Assert.Equal(2, response.Result);
        }
    }
    
    public static class GrpcExtensions
    {
        public static Task<TResponse> Execute<TRequest, TResponse>(this Channel channel, TRequest request, string serviceName, string methodName,
            CallOptions options = default, string? host = null)
            where TRequest : class
            where TResponse : class
            => Execute<TRequest, TResponse>(new DefaultCallInvoker(channel), request, serviceName, methodName, options, host);
        
        public static async Task<TResponse> Execute<TRequest, TResponse>(this CallInvoker invoker, TRequest request, string serviceName, string methodName,
            CallOptions options = default, string? host = null)
            where TRequest : class
            where TResponse : class
        {
            var method = new Method<TRequest, TResponse>(MethodType.Unary, serviceName, methodName,
                CustomMarshaller<TRequest>.Instance, CustomMarshaller<TResponse>.Instance);
            using (var auc = invoker.AsyncUnaryCall(method, host, options, request))
            {
                return await auc.ResponseAsync;
            }
        }
        
        class CustomMarshaller<T> : Marshaller<T>
        {
            public static readonly CustomMarshaller<T> Instance = new CustomMarshaller<T>();
            private CustomMarshaller() : base(Serialize, Deserialize) { }

            private static T Deserialize(byte[] payload)
            {
                using (var ms = new MemoryStream(payload))
                {
                    return ProtoBuf.Serializer.Deserialize<T>(ms);
                }
            }
            private static byte[] Serialize(T payload)
            {
                using (var ms = new MemoryStream())
                {
                    ProtoBuf.Serializer.Serialize<T>(ms, payload);
                    return ms.ToArray();
                }
            }
        }
    }    
    
}