using Grpc.Core;
using ProtoBuf.Grpc.Server;
using System;
using System.Reflection;
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
            _ = _server.Services.AddCodeFirst(new ApplyServices());
            _server.Start();
        }

        public async ValueTask DisposeAsync()
        {
            await _server.ShutdownAsync();
        }
    }
    
    public class GrpcServiceTests : IClassFixture<GrpcServiceFixture>
    {
        private readonly GrpcServiceFixture _fixture;
        public GrpcServiceTests(GrpcServiceFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task CanCallAllApplyServicesUnaryAsync()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{GrpcServiceFixture.Port}");

            var request = new Apply { X = 6, Y = 3 };
            var client = new GrpcClient(http, nameof(ApplyServices));

            Assert.Equal(nameof(ApplyServices), client.ToString());

            var response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Add));
            Assert.Equal(9, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Mul));
            Assert.Equal(18, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Sub));
            Assert.Equal(3, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Div));
            Assert.Equal(2, response.Result);
        }

        [Fact]
        public async Task CanCallAllApplyServicesTypedUnaryAsync()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{GrpcServiceFixture.Port}");

            var request = new Apply { X = 6, Y = 3 };

            var client = http.CreateGrpcService(typeof(ApplyServices));
            Assert.Equal(nameof(ApplyServices), client.ToString());

            var response = await client.UnaryAsync<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Add)));
            Assert.Equal(9, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Mul)));
            Assert.Equal(18, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Sub)));
            Assert.Equal(3, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Div)));
            Assert.Equal(2, response.Result);

            static MethodInfo GetMethod(string name) => typeof(ApplyServices).GetMethod(name)!;
        }

        [Fact]
        public void CanCallAllApplyServicesUnarySync()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{GrpcServiceFixture.Port}");

            var request = new Apply { X = 6, Y = 3 };
            var invoker = http.CreateCallInvoker();

            var client = new GrpcClient(http, nameof(ApplyServices));
            Assert.Equal(nameof(ApplyServices), client.ToString());

            var response = client.BlockingUnary<Apply, ApplyResponse>(request, nameof(ApplyServices.Add));
            Assert.Equal(9, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, nameof(ApplyServices.Mul));
            Assert.Equal(18, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, nameof(ApplyServices.Sub));
            Assert.Equal(3, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, nameof(ApplyServices.Div));
            Assert.Equal(2, response.Result);
        }

        [Fact]
        public void CanCallAllApplyServicesTypedUnarySync()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{GrpcServiceFixture.Port}");

            var request = new Apply { X = 6, Y = 3 };
            var invoker = http.CreateCallInvoker();

            var client = new GrpcClient(http, typeof(ApplyServices));
            Assert.Equal(nameof(ApplyServices), client.ToString());

            var response = client.BlockingUnary<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Add)));
            Assert.Equal(9, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Mul)));
            Assert.Equal(18, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Sub)));
            Assert.Equal(3, response.Result);
            response = client.BlockingUnary<Apply, ApplyResponse>(request, GetMethod(nameof(ApplyServices.Div)));
            Assert.Equal(2, response.Result);

            static MethodInfo GetMethod(string name) => typeof(ApplyServices).GetMethod(name)!;
        }
    }
}