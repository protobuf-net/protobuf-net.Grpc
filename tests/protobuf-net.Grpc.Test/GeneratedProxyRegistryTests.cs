using Grpc.Core;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace protobuf_net.Grpc.Test
{
    /// <summary>
    /// The runtime half of build-time proxy support: whatever the generator emits, these are the
    /// hooks it emits *into*, and this is the contract it has to satisfy.
    /// </summary>
    /// <remarks>
    /// The generator itself lives in protobuf-net.BuildTools (protobuf-net/protobuf-net#1254 and
    /// friends), so the "generated" code here is hand-written to match what it emits - deliberately,
    /// since this project must be able to test the contract without a build-time dependency on the
    /// tool that consumes it. If the two ever drift, the generator's own golden-file tests are what
    /// pin the emitted text; these pin the behaviour it relies on.
    /// </remarks>
    public class GeneratedProxyRegistryTests
    {
        [DataContract]
        public class Echo
        {
            [DataMember(Order = 1)] public string Value { get; set; } = "";
        }

        [Service]
        public interface IRegistryEchoService
        {
            ValueTask<Echo> EchoAsync(Echo value, CallContext ctx = default);
        }

        /// <summary>
        /// Stands in for the generated client proxy, including the factory shape the registry stores.
        /// </summary>
        private sealed class EchoClientProxy : ClientBase, IRegistryEchoService
        {
            private readonly Marshaller<Echo> _marshaller;
            private readonly Method<Echo, Echo> _echo;

            public EchoClientProxy(CallInvoker callInvoker, BinderConfiguration config) : base(callInvoker)
            {
                config ??= BinderConfiguration.Default;
                _marshaller = config.GetMarshaller<Echo>();
                _echo = new Method<Echo, Echo>(MethodType.Unary, "protobuf_net.Grpc.Test.RegistryEchoService", "Echo", _marshaller, _marshaller);
            }

            public static IRegistryEchoService Create(CallInvoker callInvoker, BinderConfiguration config)
                => new EchoClientProxy(callInvoker, config);

            ValueTask<Echo> IRegistryEchoService.EchoAsync(Echo value, CallContext ctx)
                => throw new NotSupportedException("not dialled in these tests");
        }

        /// <summary>
        /// Stands in for the generated server bindings, including the <c>Bind&lt;TService&gt;</c> shape
        /// the registry resolves by reflection.
        /// </summary>
        public static class EchoServerBindings
        {
            public static int Bind<TService>(IServerMethodBinder<TService> binder)
                where TService : class, IRegistryEchoService
            {
                var config = binder.Configuration;
                var count = 0;
                try
                {
                    var marshaller = config.GetMarshaller<Echo>();
                    var method = new Method<Echo, Echo>(MethodType.Unary, "protobuf_net.Grpc.Test.RegistryEchoService", "Echo", marshaller, marshaller);
                    binder.AddUnaryMethod<Echo, Echo>(
                        method,
                        binder.GetMetadata(typeof(IRegistryEchoService), nameof(IRegistryEchoService.EchoAsync)),
                        static (service, request, context) => service.EchoAsync(request).AsTask());
                    count++;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Failed to bind '" + nameof(IRegistryEchoService.EchoAsync) + "' on " + typeof(IRegistryEchoService).FullName + ": " + ex.Message, ex);
                }
                return count;
            }
        }

        // The generator emits this as a [ModuleInitializer] per contract; a static constructor stands
        // in because this project also targets net472, where that attribute doesn't exist - which is
        // the case the generator reports rather than emitting code that cannot compile.
        static GeneratedProxyRegistryTests() => Register();

        internal static void Register()
        {
            GeneratedProxyRegistry.RegisterClient<IRegistryEchoService>(EchoClientProxy.Create);
            GeneratedProxyRegistry.RegisterServer(typeof(IRegistryEchoService), typeof(EchoServerBindings));
        }

        [Fact]
        public void RegisteredClientFactoryWinsOverTheRuntimeProxy()
        {
            var client = new NullCallInvoker().CreateGrpcService<IRegistryEchoService>();

            // the registered factory is consulted before any reflection / [Proxy] / IL-emit path
            Assert.IsType<EchoClientProxy>(client);
            Assert.DoesNotContain("ProtoBuf.Grpc.Internal.Proxies", client.GetType().FullName ?? "");
        }

        [Fact]
        public void RegisteringAClientDoesNotTouchTheContractInterface()
        {
            // registration is by lookup, not by stamping [Proxy] onto the user's own interface
            Assert.Null(typeof(IRegistryEchoService).GetCustomAttribute<ProxyAttribute>());
        }

        [Fact]
        public async Task RegisteredServerBindingsWireHandlersThatDispatchThroughTheInterface()
        {
            Assert.True(GeneratedProxyRegistry.TryGetServerBindings(typeof(IRegistryEchoService), out var bindingsType));

            // the registry stores the type; the caller closes Bind<TService> over the concrete service
            var bind = bindingsType!.GetMethod("Bind", BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(typeof(EchoService));
            var binder = new RecordingBinder<EchoService>();
            var count = (int)bind.Invoke(null, [binder])!;

            Assert.Equal(1, count);
            var (method, _, handler) = Assert.Single(binder.UnaryHandlers);
            Assert.Equal(MethodType.Unary, method.Type);

            var service = new EchoService();
            var result = await handler(service, new Echo { Value = "hello" }, new FakeServerCallContext());
            Assert.Equal("hello-echoed", result.Value);
            Assert.True(service.WasCalled);
        }

        [Fact]
        public void MarshallerResolutionFailureNamesTheOperationAndKeepsTheCause()
        {
            // a factory that claims nothing - the same shape as a trimmed-away contract type, where
            // marshaller resolution is what fails
            var binder = new UnmarshallableBinder<EchoService>();

            Assert.True(GeneratedProxyRegistry.TryGetServerBindings(typeof(IRegistryEchoService), out var bindingsType));
            var bind = bindingsType!.GetMethod("Bind", BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(typeof(EchoService));

            var invocation = Assert.Throws<TargetInvocationException>(() => bind.Invoke(null, [binder]));

            // the failure has to reach the host (no silent swallow) carrying enough context for an
            // operator reading the startup log to identify the broken operation
            var error = Assert.IsType<InvalidOperationException>(invocation.InnerException);
            Assert.Contains(nameof(IRegistryEchoService.EchoAsync), error.Message);
            Assert.Contains(typeof(IRegistryEchoService).FullName!, error.Message);
            Assert.NotNull(error.InnerException);
        }

        private sealed class EchoService : IRegistryEchoService
        {
            public bool WasCalled { get; private set; }

            public ValueTask<Echo> EchoAsync(Echo value, CallContext ctx = default)
            {
                WasCalled = true;
                return new ValueTask<Echo>(new Echo { Value = value.Value + "-echoed" });
            }
        }

        private sealed class RecordingBinder<TService> : IServerMethodBinder<TService> where TService : class
        {
            public BinderConfiguration Configuration => BinderConfiguration.Default;

            public List<(Method<Echo, Echo> Method, IList<object> Metadata, UnaryServerHandler<TService, Echo, Echo> Handler)> UnaryHandlers { get; } = new();

            public IList<object> GetMetadata(Type contractType, string methodName) => Array.Empty<object>();

            public void AddUnaryMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, IList<object> metadata, UnaryServerHandler<TService, TRequest, TResponse> handler)
                where TRequest : class where TResponse : class
            {
                if (typeof(TRequest) == typeof(Echo) && typeof(TResponse) == typeof(Echo))
                {
                    UnaryHandlers.Add((
                        (Method<Echo, Echo>)(object)method,
                        metadata,
                        (UnaryServerHandler<TService, Echo, Echo>)(object)handler));
                }
            }

            public void AddServerStreamingMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, IList<object> metadata, ServerStreamingServerHandler<TService, TRequest, TResponse> handler)
                where TRequest : class where TResponse : class { }

            public void AddClientStreamingMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, IList<object> metadata, ClientStreamingServerHandler<TService, TRequest, TResponse> handler)
                where TRequest : class where TResponse : class { }

            public void AddDuplexStreamingMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, IList<object> metadata, DuplexStreamingServerHandler<TService, TRequest, TResponse> handler)
                where TRequest : class where TResponse : class { }
        }

        private sealed class UnmarshallableBinder<TService> : IServerMethodBinder<TService> where TService : class
        {
            public BinderConfiguration Configuration { get; } = BinderConfiguration.Create(
                marshallerFactories: [new NothingMarshallerFactory()]);

            public IList<object> GetMetadata(Type contractType, string methodName) => Array.Empty<object>();

            public void AddUnaryMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, IList<object> metadata, UnaryServerHandler<TService, TRequest, TResponse> handler)
                where TRequest : class where TResponse : class { }

            public void AddServerStreamingMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, IList<object> metadata, ServerStreamingServerHandler<TService, TRequest, TResponse> handler)
                where TRequest : class where TResponse : class { }

            public void AddClientStreamingMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, IList<object> metadata, ClientStreamingServerHandler<TService, TRequest, TResponse> handler)
                where TRequest : class where TResponse : class { }

            public void AddDuplexStreamingMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, IList<object> metadata, DuplexStreamingServerHandler<TService, TRequest, TResponse> handler)
                where TRequest : class where TResponse : class { }
        }

        private sealed class NothingMarshallerFactory : MarshallerFactory
        {
            protected internal override bool CanSerialize(Type type) => false;
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

        // enough of a CallInvoker to construct a proxy without standing up a server
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
