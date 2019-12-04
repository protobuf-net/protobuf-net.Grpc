#if !NETFRAMEWORK
using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Server;
using Xunit;

namespace protobuf_net.Grpc.Test.Integration
{

    [DataContract(Name = "MyDataContract")]
    public class ClassContract
    {
        [DataMember(Order = 1)] public string S = string.Empty;
        [DataMember(Order = 2)] public int N;
    }

    [DataContract(Name = "MyDataContract")]
    public struct StructContract
    {
        [DataMember(Order = 1)] public string S;
        [DataMember(Order = 2)] public int N;
    }

    [ServiceContract(Name = "MyServiceContract")]
    public interface IMyServiceContract
    {
        ClassContract GetContract1(int n);
        ValueTask<ClassContract> GetContract1Async(int n);
        StructContract GetContract2();
        Task<StructContract> GetContract2Async();
        Task<bool> SaveContract1(ClassContract contract);
        ValueTask<bool> SaveContract2(StructContract contract);
    }

    public class MyServiceContract : IMyServiceContract
    {
        public ClassContract GetContract1(int n)
        {
            return new ClassContract { S = "foo", N = n };
        }

        public ValueTask<ClassContract> GetContract1Async(int n)
        {
            return new ValueTask<ClassContract>(GetContract1(n));
        }

        public StructContract GetContract2()
        {
            return new StructContract { S = "foo", N = 42 };
        }

        public Task<StructContract> GetContract2Async()
        {
            return Task.FromResult(GetContract2());
        }

        public Task<bool> SaveContract1(ClassContract contract)
        {
            return Task.FromResult(true);
        }
        public ValueTask<bool> SaveContract2(StructContract contract)
        {
            return new ValueTask<bool>(false);
        }
    }

    [ServiceContract(Name = "MyServiceContract")]
    public interface IMyServiceContractClient
    {
        // swap actual types returned
        StructContract GetContract1(int n);
        ClassContract GetContract2();
        Task<StructContract> GetContract1Async(int n);
        ValueTask<ClassContract> GetContract2Async();
        ValueTask<bool> SaveContract1(StructContract contract);
        Task<bool> SaveContract2(ClassContract contract);
    }

    public class GrpcService2Fixture : IAsyncDisposable
    {
        public const int Port = 10043;
        private readonly Server _server;

        public GrpcService2Fixture()
        {
            _server = new Server
            {
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            int opCount = _server.Services.AddCodeFirst(new MyServiceContract());
            _server.Start();
        }

        public async ValueTask DisposeAsync()
        {
            await _server.ShutdownAsync();
        }
    }
    
    public class StructClassCompatibilityTest : IClassFixture<GrpcService2Fixture>
    {
        private GrpcService2Fixture _fixture;
        public StructClassCompatibilityTest(GrpcService2Fixture fixture) => _fixture = fixture;

        [Fact]
        public async Task ValueTypeContractIsCompatibleWithClass()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            using var http = GrpcChannel.ForAddress($"http://localhost:{GrpcService2Fixture.Port}");
            IMyServiceContractClient proxy = http.CreateGrpcService<IMyServiceContractClient>();

            var result1 = proxy.GetContract1(99);
            Assert.IsType<StructContract>(result1);
            Assert.Equal("foo", result1.S);
            Assert.Equal(99, result1.N);

            var result2 = proxy.GetContract2();
            Assert.IsType<ClassContract>(result2);
            Assert.Equal("foo", result2.S);
            Assert.Equal(42, result2.N);

            var result3 = await proxy.GetContract1Async(12);
            Assert.IsType<StructContract>(result3);
            Assert.Equal("foo", result3.S);
            Assert.Equal(12, result3.N);

            var result4 = await proxy.GetContract2Async();
            Assert.IsType<ClassContract>(result4);
            Assert.Equal("foo", result4.S);
            Assert.Equal(42, result4.N);

            bool result5 = await proxy.SaveContract1(new StructContract { S = "bar", N = 33 });
            Assert.True(result5);

            bool result6 = await proxy.SaveContract2(new ClassContract { S = "bar", N = 33 });
            Assert.False(result6);
        }
    }
}
#endif
