using Grpc.Core;
using ProtoBuf;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using ProtoBuf.Grpc;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace protobuf_net.Grpc.Test.Integration
{
    public class ClientProxyTests : IClassFixture<ClientProxyTests.ClientProxyTestsServerFixture>
    {

        [DataContract]
        public class MyRequest
        {
            [DataMember(Order = 1)]
            public int Id { get; set; }
        }

        [DataContract]
        public class MyResponse
        {
            [DataMember(Order = 1)]
            public string? Value { get; set; }
        }
       
       
        /// <summary>
        /// An interface which is not marked with [Service] attribute.
        /// Its methods' proxy-implementations are expected to throw unsupported exception.
        /// </summary>
        public interface INotAService
        {
            ValueTask<MyResponse> NotAServiceUnary(MyRequest request, CallContext callContext = default);
        }

        [SubService]
        public interface ISomeInheritableBaseGenericService<in TGenericRequest, TGenericResult>
        {
            ValueTask<TGenericResult> BaseGenericUnary(TGenericRequest request, CallContext callContext = default);
        }
        
        [Service]
        public interface IMyDerivedService : ISomeInheritableBaseGenericService<MyRequest, MyResponse>, INotAService
        {
            ValueTask<MyResponse> Derived(MyRequest request, CallContext callContext = default);
        }

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0051, IDE0052 // "unused" things; they are, but it depends on the TFM
        private readonly ITestOutputHelper _log;
        private readonly ClientProxyTestsServerFixture _server;
        private void Log(string message) => _log?.WriteLine(message);
#pragma warning restore IDE0051, IDE0052
#pragma warning restore IDE0079 // Remove unnecessary suppression

        private int Port => _server.Port;

        public ClientProxyTests(ClientProxyTestsServerFixture server, ITestOutputHelper log)
        {
            _server = server;
            _log = log;
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
        }

        public class ClientProxyTestsServerFixture : IMyDerivedService, IAsyncDisposable
        {
            public int Port { get; } = PortManager.GetNextPort();

            public async ValueTask DisposeAsync()
            {
                if (_server != null)
                    await _server.KillAsync();
            }

            private readonly Server? _server;
            public ClientProxyTestsServerFixture()
            {
                _server = new Server
                {
                    Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
                };
                _server.Services.AddCodeFirst(this);
                _server.Start();
            }

            public async ValueTask<MyResponse> NotAServiceUnary(MyRequest request, CallContext callContext = default)
            {
                // we expect this won't be called through GRPC
                return await Task.FromResult(new MyResponse());
            }

            public async ValueTask<MyResponse> Derived(MyRequest request, CallContext callContext = default)
            {
                return await Task.FromResult(new MyResponse());
            }

            public async ValueTask<MyResponse> BaseGenericUnary(MyRequest request, CallContext callContext = default)
            {
                return await Task.FromResult(new MyResponse());
            }
        }


#if !(NET461 || NET472)
        [Fact]
        public async Task ClientProxyTests_WhenCalledToDerivedInterfaceMethod_NoException()
        {
            using var http = global::Grpc.Net.Client.GrpcChannel.ForAddress($"http://localhost:{Port}");
            var client = http.CreateGrpcService<IMyDerivedService>();
            var obj = await client.Derived(new MyRequest());
        }

        [Fact]
        public async Task ClientProxyTests_WhenCalledToBaseInheritableMethod_NoException()
        {
            using var http = global::Grpc.Net.Client.GrpcChannel.ForAddress($"http://localhost:{Port}");
            var client = http.CreateGrpcService<IMyDerivedService>();
            var obj = await client.BaseGenericUnary(new MyRequest());
        }


        [Fact]
        public async Task ClientProxyTests_WhenCalledToBaseNonInheritableMethod_ThrowsUnsupportedException()
        {
            using var http = global::Grpc.Net.Client.GrpcChannel.ForAddress($"http://localhost:{Port}");
            var client = http.CreateGrpcService<IMyDerivedService>();

            await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                // we expect an exception from the client proxy
                var obj = await client.NotAServiceUnary(new MyRequest());
            });
        }
#endif
    }        

}