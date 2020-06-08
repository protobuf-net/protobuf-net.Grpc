# if NETCOREAPP3_1
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
using Grpc.Core.Interceptors;
using Xunit.Abstractions;
using System.Runtime.CompilerServices;
using ProtoBuf.Meta;

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

    [Service]
    public interface IInterceptedService
    {
        ValueTask<ApplyResponse> Add(Apply request);
    }
    public class InterceptedService : IInterceptedService
    {
        public ValueTask<ApplyResponse> Add(Apply request) => new ValueTask<ApplyResponse>(new ApplyResponse(request.X + request.Y));
    }

    [Serializable]
    public class AdhocRequest
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
    [Serializable]
    public class AdhocResponse
    {
        public int Z { get; set; }
    }
    [Service]
    public interface IAdhocService
    {
        AdhocResponse AdhocMethod(AdhocRequest request);
    }

    public class AdhocService : IAdhocService
    {
        public AdhocResponse AdhocMethod(AdhocRequest request)
            => new AdhocResponse { Z = request.X + request.Y };
    }

    static class AdhocConfig
    {
        public static ClientFactory ClientFactory { get; }
            = ClientFactory.Create(BinderConfiguration.Create(new[] {
                    // we'll allow multiple marshallers to take a stab; protobuf-net first,
                    // then try BinaryFormatter for anything that protobuf-net can't handle

                    ProtoBufMarshallerFactory.Default,
#pragma warning disable CS0618 // Type or member is obsolete
                    BinaryFormatterMarshallerFactory.Default, // READ THE NOTES ON NOT DOING THIS
#pragma warning restore CS0618 // Type or member is obsolete
                    }));
    }

    public class GrpcServiceFixture : IAsyncDisposable
    {
        public const int Port = 10042;
        private readonly Server _server;

        private readonly Interceptor _interceptor;
        public ITestOutputHelper? Output { get; set; }
        public void Log(string message) => Output?.WriteLine(message);
        public GrpcServiceFixture()
        {
            _interceptor = new TestInterceptor(this);
#pragma warning disable CS0618 // Type or member is obsolete
            BinaryFormatterMarshallerFactory.I_Have_Read_The_Notes_On_Not_Using_BinaryFormatter = true;
            BinaryFormatterMarshallerFactory.I_Promise_Not_To_Do_This = true; // signed: Marc Gravell
#pragma warning restore CS0618 // Type or member is obsolete

            _server = new Server
            {
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            _server.Services.AddCodeFirst(new ApplyServices());
            _server.Services.AddCodeFirst(new AdhocService(), AdhocConfig.ClientFactory);
            _server.Services.AddCodeFirst(new InterceptedService() , interceptors: new[] { _interceptor });
            _server.Start();
        }

        public async ValueTask DisposeAsync()
        {
            await _server.ShutdownAsync();
        }
    }
    public class TestInterceptor : Interceptor
    {
        private readonly GrpcServiceFixture _parent;
        public void Log(string message) => _parent?.Log(message);
        public TestInterceptor(GrpcServiceFixture parent) => _parent = parent;
        private static string Me([CallerMemberName] string? caller = null) => caller ?? "(unknown)";
        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            Log($"> {Me()}");
            var result = await base.UnaryServerHandler(request, context, continuation);
            Log($"< {Me()}");
            return result;
        }
    }


    public class GrpcServiceTests : IClassFixture<GrpcServiceFixture>, IDisposable
    {
        private readonly GrpcServiceFixture _fixture;
        public GrpcServiceTests(GrpcServiceFixture fixture, ITestOutputHelper log)
        {
            _fixture = fixture;
            if (fixture != null) fixture.Output = log;
        }

        private void Log(string message) => _fixture?.Log(message);

        public void Dispose()
        {
            if (_fixture != null) _fixture.Output = null;
        }

        private static readonly BinderConfiguration DisableContextualSerializer = BinderConfiguration.Create(
            new[] { ProtoBufMarshallerFactory.Create(options: ProtoBufMarshallerFactory.Options.DisableContextualSerializer) });

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CanCallAllApplyServicesUnaryAsync(bool disableContextual)
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{GrpcServiceFixture.Port}");

            var request = new Apply { X = 6, Y = 3 };
            var client = new GrpcClient(http, nameof(ApplyServices), disableContextual ? DisableContextualSerializer : null);

            Assert.Equal(nameof(ApplyServices), client.ToString());

#if DEBUG
            var uplevelReadsBefore = ProtoBufMarshallerFactory.UplevelBufferReadCount;
            var uplevelWritesBefore = ProtoBufMarshallerFactory.UplevelBufferWriteCount;
            Log($"Buffer usage before: {uplevelReadsBefore}/{uplevelWritesBefore}");
#endif

            var response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Add));
            Assert.Equal(9, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Mul));
            Assert.Equal(18, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Sub));
            Assert.Equal(3, response.Result);
            response = await client.UnaryAsync<Apply, ApplyResponse>(request, nameof(ApplyServices.Div));
            Assert.Equal(2, response.Result);

#if DEBUG
            var uplevelReadsAfter = ProtoBufMarshallerFactory.UplevelBufferReadCount;
            var uplevelWritesAfter = ProtoBufMarshallerFactory.UplevelBufferWriteCount;
            Log($"Buffer usage after: {uplevelReadsAfter}/{uplevelWritesAfter}");

#if PROTOBUFNET_BUFFERS
            bool expectContextual = true;
#else
            bool expectContextual = false;
#endif
            if (disableContextual) expectContextual = false;

            if (expectContextual)
            {
                Assert.True(uplevelReadsBefore < uplevelReadsAfter);
                Assert.True(uplevelWritesBefore < uplevelWritesAfter);
            }
            else
            {
                Assert.Equal(uplevelReadsBefore, uplevelReadsAfter);
                Assert.Equal(uplevelWritesBefore, uplevelWritesAfter);
            }
#endif
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

        [Fact]
        public void CanCallAdocService()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{GrpcServiceFixture.Port}");

            var request = new AdhocRequest { X = 12, Y = 7 };
            var client = http.CreateGrpcService<IAdhocService>(AdhocConfig.ClientFactory);
            var response = client.AdhocMethod(request);
            Assert.Equal(19, response.Z);
        }

        [Fact]
        public async Task CanCallInterceptedService()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{GrpcServiceFixture.Port}");

            var request = new Apply { X = 6, Y = 3 };

            var client = http.CreateGrpcService<IInterceptedService>();
            _fixture?.Log("> Add");
            var result = await client.Add(new Apply { X = 42, Y = 8 });
            _fixture?.Log("< Add");
            Assert.Equal(50, result.Result);
        }
    }
}
#endif