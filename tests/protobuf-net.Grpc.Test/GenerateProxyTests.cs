using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Grpc.Core;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using Xunit;

namespace protobuf_net.Grpc.Test
{
    [DataContract]
    public class GenProxyEcho
    {
        [DataMember(Order = 1)] public string Value { get; set; } = "";
    }

    [Service]
    [GenerateProxy]
    public partial interface IGenProxyService
    {
        ValueTask<GenProxyEcho> EchoAsync(GenProxyEcho value, CallContext ctx = default);
    }

    public class GenerateProxyTests
    {
        [Fact]
        public void GeneratedProxyIsRegisteredViaProxyAttribute()
        {
            // The source generator should stamp [Proxy(typeof(...))] onto a partial declaration of the interface
            var proxyAttr = typeof(IGenProxyService).GetCustomAttribute<ProxyAttribute>();
            Assert.NotNull(proxyAttr);
            Assert.NotNull(proxyAttr!.Type);
            Assert.True(typeof(IGenProxyService).IsAssignableFrom(proxyAttr.Type));
        }

        [Fact]
        public void GeneratedProxyExposesStaticCreateFactory()
        {
            var proxyType = typeof(IGenProxyService).GetCustomAttribute<ProxyAttribute>()!.Type;
            var create = proxyType.GetMethod(
                "Create",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(CallInvoker)],
                modifiers: null);
            Assert.NotNull(create);
            Assert.True(typeof(IGenProxyService).IsAssignableFrom(create!.ReturnType));
        }

        [Fact]
        public void CreateGrpcServiceReturnsGeneratedProxyInstance()
        {
            var invoker = new NullCallInvoker();
            var client = invoker.CreateGrpcService<IGenProxyService>();

            var proxyType = typeof(IGenProxyService).GetCustomAttribute<ProxyAttribute>()!.Type;
            Assert.IsType(proxyType, client);

            // it should NOT be an IL-emitted proxy (those live under ProtoBuf.Grpc.Internal.Proxies.*)
            Assert.DoesNotContain("ProtoBuf.Grpc.Internal.Proxies", client.GetType().FullName ?? "");
        }

        [Fact]
        public void GeneratedServerBindingsAreRegisteredViaAttribute()
        {
            var attr = typeof(IGenProxyService).GetCustomAttribute<GeneratedServerAttribute>();
            Assert.NotNull(attr);
            Assert.NotNull(attr!.Type);

            // generated bindings type should expose a public static Bind<TService>(IServerMethodBinder<TService>) method
            var bind = attr.Type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .SingleOrDefault(m => m.Name == "Bind" && m.IsGenericMethodDefinition);
            Assert.NotNull(bind);
            var parameters = bind!.GetParameters();
            Assert.Single(parameters);
            // parameter type is IServerMethodBinder<TService>
            Assert.True(parameters[0].ParameterType.IsGenericType);
            Assert.Equal(typeof(IServerMethodBinder<>), parameters[0].ParameterType.GetGenericTypeDefinition());
        }

        [Fact]
        public async Task GeneratedBindWiresUnaryToHandlerThatDispatchesThroughInterface()
        {
            // a fake binder records what the generated code asks to register
            var binder = new RecordingBinder<MyEchoService>();

            // close Bind<MyEchoService> against our concrete impl and invoke
            var attr = typeof(IGenProxyService).GetCustomAttribute<GeneratedServerAttribute>();
            var bindMethod = attr!.Type.GetMethod("Bind", BindingFlags.Public | BindingFlags.Static)!
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
