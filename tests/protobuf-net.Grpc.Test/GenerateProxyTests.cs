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
