using Grpc.Core;
using ProtoBuf.Grpc.Server;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using Xunit;
using Xunit.Abstractions;

#if MANAGED_CLIENT
using Grpc.Net.Client;
#endif

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
        private readonly GrpcServiceFixture _fixture;
        private readonly ITestOutputHelper _log;
        public GrpcServiceTests(ITestOutputHelper log, GrpcServiceFixture fixture)
        {
            _fixture = fixture;
            _log = log;
        }

        private void Log(string message) => _log?.WriteLine(message);

        [Fact]
        public async Task CanCallAllApplyServices_NativeClient()
        {
            var channel = new Channel("localhost", GrpcServiceFixture.Port, ChannelCredentials.Insecure);
            try
            {
                await TestMathAsync(channel);
            }
            finally
            {
                await channel.ShutdownAsync();
            }
        }

        private async Task TestMathAsync(ChannelBase channel)
        {
            var invoker = channel.CreateCallInvoker();
            var request = new Apply { X = 6, Y = 3 };

#if DEBUG
            var uplevelReadsBefore = ProtoBufMarshallerFactory.UplevelBufferReadCount;
            var uplevelWritesBefore = ProtoBufMarshallerFactory.UplevelBufferWriteCount;
            Log($"Buffer usage before: {uplevelReadsBefore}/{uplevelWritesBefore}");
#endif

            var response = await invoker.Execute<Apply, ApplyResponse>(request, nameof(ApplyServices), nameof(ApplyServices.Add));
            Assert.Equal(9, response.Result);
            response = await invoker.Execute<Apply, ApplyResponse>(request, nameof(ApplyServices), nameof(ApplyServices.Mul));
            Assert.Equal(18, response.Result);
            response = await invoker.Execute<Apply, ApplyResponse>(request, nameof(ApplyServices), nameof(ApplyServices.Sub));
            Assert.Equal(3, response.Result);
            response = await invoker.Execute<Apply, ApplyResponse>(request, nameof(ApplyServices), nameof(ApplyServices.Div));
            Assert.Equal(2, response.Result);

#if DEBUG
            var uplevelReadsAfter = ProtoBufMarshallerFactory.UplevelBufferReadCount;
            var uplevelWritesAfter = ProtoBufMarshallerFactory.UplevelBufferWriteCount;
            Log($"Buffer usage after: {uplevelReadsAfter}/{uplevelWritesAfter}");

#if PROTOBUFNET_BUFFERS
            Assert.True(uplevelReadsBefore < uplevelReadsAfter);
            Assert.True(uplevelWritesBefore < uplevelWritesAfter);
#else
            Assert.Equal(uplevelReadsBefore, uplevelReadsAfter);
            Assert.Equal(uplevelWritesBefore, uplevelWritesAfter);
#endif

#endif
        }

#if MANAGED_CLIENT
        [Fact]
        public async Task CanCallAllApplyServices_ManagedClient()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{GrpcServiceFixture.Port}");
            await TestMathAsync(http);


        }
#endif // MANAGED_CLIENT
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
            var config = BinderConfiguration.Default;
            var method = new Method<TRequest, TResponse>(MethodType.Unary, serviceName, methodName,
                config.GetMarshaller<TRequest>(), config.GetMarshaller<TResponse>());
            
            using var auc = invoker.AsyncUnaryCall(method, host, options, request);
            return await auc.ResponseAsync;
        }
    }

}