using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Grpc.Core;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Internal;
using Xunit;

namespace protobuf_net.Grpc.Test
{
    [DataContract]
    public class GenProxyEcho
    {
        [DataMember(Order = 1)] public string Value { get; set; } = "";
    }

    // Note: no [GenerateProxy], no partial — the source generator auto-detects [Service] interfaces
    // and registers the generated proxy + server bindings via [ModuleInitializer].
    [Service]
    public interface IGenProxyService
    {
        ValueTask<GenProxyEcho> EchoAsync(GenProxyEcho value, CallContext ctx = default);
    }

    public class GenerateProxyTests
    {
        [Fact]
        public void GeneratedClientFactoryRegisteredInRegistry()
        {
            // The [ModuleInitializer] emitted by the generator populates GeneratedProxyRegistry.
            // CreateGrpcService<T> consults it before any reflection / [Proxy] / IL emit path.
            var invoker = new NullCallInvoker();
            var client = invoker.CreateGrpcService<IGenProxyService>();
            Assert.NotNull(client);

            // proxy is the build-time generated one, NOT an IL-emitted runtime proxy.
            Assert.DoesNotContain("ProtoBuf.Grpc.Internal.Proxies", client.GetType().FullName ?? "");
            Assert.Equal("ProtoBuf.Grpc.Generated", client.GetType().Namespace);
        }

        [Fact]
        public void NoProxyAttributeStampedOnInterface()
        {
            // The generator no longer stamps anything on the user's interface — registration happens
            // via the registry at module-init time, so neither attribute should appear here.
#pragma warning disable CS0618 // GenerateProxyAttribute / GeneratedServerAttribute are obsolete
            Assert.Null(typeof(IGenProxyService).GetCustomAttribute<ProxyAttribute>());
            Assert.Null(typeof(IGenProxyService).GetCustomAttribute<GenerateProxyAttribute>());
            Assert.Null(typeof(IGenProxyService).GetCustomAttribute<GeneratedServerAttribute>());
#pragma warning restore CS0618
        }

        [Fact]
        public async Task GeneratedBindWiresUnaryToHandlerThatDispatchesThroughInterface()
        {
            // a fake binder records what the generated code asks to register
            var binder = new RecordingBinder<MyEchoService>();

            // resolve the generated bindings type via the registry, then close Bind<MyEchoService>
            Assert.True(GeneratedProxyRegistry.TryGetServerBindings(typeof(IGenProxyService), out var bindingsType));
            var bindMethod = bindingsType!.GetMethod("Bind", BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(typeof(MyEchoService));
            var count = (int)bindMethod.Invoke(null, [binder])!;

            Assert.Equal(1, count);
            Assert.Single(binder.UnaryHandlers);

            // invoke the recorded handler and verify it routes through MyEchoService.EchoAsync
            var svc = new MyEchoService();
            var (_, _, handler) = binder.UnaryHandlers[0];
            var result = await handler(svc, new GenProxyEcho { Value = "hello" }, new FakeServerCallContext());
            Assert.Equal("hello-echoed", result.Value);
            Assert.True(svc.WasCalled);
        }

        private sealed class MyEchoService : IGenProxyService
        {
            public bool WasCalled { get; private set; }
            public ValueTask<GenProxyEcho> EchoAsync(GenProxyEcho value, CallContext ctx = default)
            {
                WasCalled = true;
                return new ValueTask<GenProxyEcho>(new GenProxyEcho { Value = value.Value + "-echoed" });
            }
        }

        private sealed class RecordingBinder<TService> : IServerMethodBinder<TService> where TService : class
        {
            public BinderConfiguration Configuration => BinderConfiguration.Default;
            public System.Collections.Generic.List<(Method<GenProxyEcho, GenProxyEcho>, System.Collections.Generic.IList<object>, UnaryServerHandler<TService, GenProxyEcho, GenProxyEcho>)> UnaryHandlers { get; } = new();

            public System.Collections.Generic.IList<object> GetMetadata(Type contractType, string methodName) => Array.Empty<object>();

            public void AddUnaryMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, System.Collections.Generic.IList<object> metadata, UnaryServerHandler<TService, TRequest, TResponse> handler)
                where TRequest : class where TResponse : class
            {
                // we only stash unary GenProxyEcho->GenProxyEcho handlers for this test
                if (typeof(TRequest) == typeof(GenProxyEcho) && typeof(TResponse) == typeof(GenProxyEcho))
                {
                    UnaryHandlers.Add((
                        (Method<GenProxyEcho, GenProxyEcho>)(object)method,
                        metadata,
                        (UnaryServerHandler<TService, GenProxyEcho, GenProxyEcho>)(object)handler));
                }
            }

            public void AddServerStreamingMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, System.Collections.Generic.IList<object> metadata, ServerStreamingServerHandler<TService, TRequest, TResponse> handler)
                where TRequest : class where TResponse : class { }
            public void AddClientStreamingMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, System.Collections.Generic.IList<object> metadata, ClientStreamingServerHandler<TService, TRequest, TResponse> handler)
                where TRequest : class where TResponse : class { }
            public void AddDuplexStreamingMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, System.Collections.Generic.IList<object> metadata, DuplexStreamingServerHandler<TService, TRequest, TResponse> handler)
                where TRequest : class where TResponse : class { }
        }

        private sealed class FakeServerCallContext : ServerCallContext
        {
            protected override string MethodCore => "Echo";
            protected override string HostCore => "";
            protected override string PeerCore => "";
            protected override DateTime DeadlineCore => DateTime.MaxValue;
            protected override Metadata RequestHeadersCore => new();
            protected override System.Threading.CancellationToken CancellationTokenCore => System.Threading.CancellationToken.None;
            protected override Metadata ResponseTrailersCore => new();
            protected override Status StatusCore { get; set; }
            protected override WriteOptions? WriteOptionsCore { get; set; }
            protected override AuthContext AuthContextCore => null!;
            protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
            protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => null!;
        }

        // minimal CallInvoker so we can construct the proxy without spinning up a server
        private sealed class NullCallInvoker : CallInvoker
        {
            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotImplementedException();
            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotImplementedException();
            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotImplementedException();
            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotImplementedException();
            public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotImplementedException();
        }
    }
}
