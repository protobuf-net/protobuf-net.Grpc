using Grpc.Core;
using ProtoBuf;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Server;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace protobuf_net.Grpc.Test.Integration.Issues
{
    public class Issue75 : IClassFixture<Issue75.Issue75ServerFixture>
    {
        const int Port = 12316;

        [Service]
        public interface IFaultTest
        {
            ValueTask VanillaFaultHanding_Success();
            ValueTask VanillaFaultHanding_Fault();
            [SimpleRpcExceptions]
            ValueTask SimplifiedFaultHandling_Success();
            [SimpleRpcExceptions]
            ValueTask SimplifiedFaultHandling_Fault();
        }

        [Service]
        public interface IInterceptedFaultTest
        {
            ValueTask Success();
            ValueTask Fault();
        }

        public Issue75(Issue75ServerFixture _)
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
        }

        public class Issue75ServerFixture : IFaultTest, IInterceptedFaultTest, IAsyncDisposable
        {
            public ValueTask DisposeAsync() => new ValueTask(_server.KillAsync());

            private readonly Server? _server;
            public Issue75ServerFixture()
            {
                _server = new Server
                {
                    Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
                };
                _server.Services.AddCodeFirst<IFaultTest>(this);
                _server.Services.AddCodeFirst<IInterceptedFaultTest>(this, interceptors: new[] { SimpleRpcExceptionsInterceptor.Instance });
                _server.Start();
            }
            async ValueTask IFaultTest.VanillaFaultHanding_Fault()
            {
                await Task.Yield();
                throw new ArgumentOutOfRangeException("foo");
            }
            ValueTask IFaultTest.VanillaFaultHanding_Success() => default;

            async ValueTask IFaultTest.SimplifiedFaultHandling_Fault()
            {
                await Task.Yield();
                throw new ArgumentOutOfRangeException("foo");
            }
            ValueTask IFaultTest.SimplifiedFaultHandling_Success() => default;

            ValueTask IInterceptedFaultTest.Success() => default;

            ValueTask IInterceptedFaultTest.Fault() => throw new ArgumentOutOfRangeException("foo");
        }

        [Fact]
        public async Task UnmanagedClient_Intercepted_Fault()
        {
            var channel = new Channel("localhost", Port, ChannelCredentials.Insecure);
            try
            {
                var client = channel.CreateGrpcService<IInterceptedFaultTest>();
                var ex = await Assert.ThrowsAsync<RpcException>(async () => await client.Fault());
                Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
                Assert.Equal(s_ExpectedMessage, ex.Status.Detail);
                Assert.Equal($"Status(StatusCode=InvalidArgument, Detail=\"{s_ExpectedMessage}\")", ex.Message);
            }
            finally
            {
                await channel.ShutdownAsync();
            }
        }

        [Fact]
        public async Task UnmanagedClient_Intercepted_Success()
        {
            var channel = new Channel("localhost", Port, ChannelCredentials.Insecure);
            try
            {
                var client = channel.CreateGrpcService<IInterceptedFaultTest>();
                await client.Success();
            }
            finally
            {
                await channel.ShutdownAsync();
            }
        }

        [Fact]
        public async Task UnmanagedClient_Vanilla_Fault()
        {
            var channel = new Channel("localhost", Port, ChannelCredentials.Insecure);
            try
            {
                var client = channel.CreateGrpcService<IFaultTest>();
                var ex = await Assert.ThrowsAsync<RpcException>(async () => await client.VanillaFaultHanding_Fault());
                Assert.Equal(StatusCode.Unknown, ex.StatusCode);
                Assert.Equal("Exception was thrown by handler.", ex.Status.Detail);
                Assert.Equal("Status(StatusCode=Unknown, Detail=\"Exception was thrown by handler.\")", ex.Message);
            }
            finally
            {
                await channel.ShutdownAsync();
            }
        }

        [Fact]
        public async Task UnmanagedClient_Vanilla_Success()
        {
            var channel = new Channel("localhost", Port, ChannelCredentials.Insecure);
            try
            {
                var client = channel.CreateGrpcService<IFaultTest>();
                await client.VanillaFaultHanding_Success();
            }
            finally
            {
                await channel.ShutdownAsync();
            }
        }

        static readonly string s_ExpectedMessage = new ArgumentOutOfRangeException("foo").Message; // can vary by runtime
        [Fact]
        public async Task UnmanagedClient_Simplified_Fault()
        {
            var channel = new Channel("localhost", Port, ChannelCredentials.Insecure);
            try
            {
                var client = channel.CreateGrpcService<IFaultTest>();
                var ex = await Assert.ThrowsAsync<RpcException>(async () => await client.SimplifiedFaultHandling_Fault());
                Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
                Assert.Equal(s_ExpectedMessage, ex.Status.Detail);
                Assert.Equal($"Status(StatusCode=InvalidArgument, Detail=\"{s_ExpectedMessage}\")", ex.Message);
            }
            finally
            {
                await channel.ShutdownAsync();
            }
        }

        [Fact]
        public async Task UnmanagedClient_Simplified_Success()
        {
            var channel = new Channel("localhost", Port, ChannelCredentials.Insecure);
            try
            {
                var client = channel.CreateGrpcService<IFaultTest>();
                await client.SimplifiedFaultHandling_Success();
            }
            finally
            {
                await channel.ShutdownAsync();
            }
        }

#if NETCOREAPP3_1
        [Fact]
        public async Task ManagedClient_Vanilla_Fault()
        {
            using var http = global::Grpc.Net.Client.GrpcChannel.ForAddress($"http://localhost:{Port}");
            var client = http.CreateGrpcService<IFaultTest>();
            var ex = await Assert.ThrowsAsync<RpcException>(async () => await client.VanillaFaultHanding_Fault());
            Assert.Equal(StatusCode.Unknown, ex.StatusCode);
            Assert.Equal("Exception was thrown by handler.", ex.Status.Detail);
            Assert.Equal("Status(StatusCode=Unknown, Detail=\"Exception was thrown by handler.\")", ex.Message);
        }

        [Fact]
        public async Task ManagedClient_Vanilla_Success()
        {
            using var http = global::Grpc.Net.Client.GrpcChannel.ForAddress($"http://localhost:{Port}");
            var client = http.CreateGrpcService<IFaultTest>();
            await client.VanillaFaultHanding_Success();
        }

        [Fact]
        public async Task ManagedClient_Simplified_Fault()
        {
            using var http = global::Grpc.Net.Client.GrpcChannel.ForAddress($"http://localhost:{Port}");
            var client = http.CreateGrpcService<IFaultTest>();
            var ex = await Assert.ThrowsAsync<RpcException>(async () => await client.SimplifiedFaultHandling_Fault());
            Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
            Assert.Equal(s_ExpectedMessage, ex.Status.Detail);
            Assert.Equal($"Status(StatusCode=InvalidArgument, Detail=\"{s_ExpectedMessage}\")", ex.Message);
        }

        [Fact]
        public async Task ManagedClient_Simplified_Success()
        {
            using var http = global::Grpc.Net.Client.GrpcChannel.ForAddress($"http://localhost:{Port}");
            var client = http.CreateGrpcService<IFaultTest>();
            await client.SimplifiedFaultHandling_Success();
        }

        [Fact]
        public async Task ManagedClient_Intercepted_Fault()
        {
            using var http = global::Grpc.Net.Client.GrpcChannel.ForAddress($"http://localhost:{Port}");
            var client = http.CreateGrpcService<IInterceptedFaultTest>();
            var ex = await Assert.ThrowsAsync<RpcException>(async () => await client.Fault());
            Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
            Assert.Equal(s_ExpectedMessage, ex.Status.Detail);
            Assert.Equal($"Status(StatusCode=InvalidArgument, Detail=\"{s_ExpectedMessage}\")", ex.Message);
        }

        [Fact]
        public async Task ManagedClient_Intercepted_Success()
        {
            using var http = global::Grpc.Net.Client.GrpcChannel.ForAddress($"http://localhost:{Port}");
            var client = http.CreateGrpcService<IInterceptedFaultTest>();
            await client.Success();
        }
#endif
    }
}
