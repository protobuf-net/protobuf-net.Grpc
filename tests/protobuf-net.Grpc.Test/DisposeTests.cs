using Grpc.Core;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Configuration;
using System;
using System.Threading.Tasks;
using Xunit;

namespace protobuf_net.Grpc.Test
{
    public class DisposeTests
    {
        [Service]
        public interface IDisposableService : IDisposable, IAsyncDisposable
        {}

        [Fact]
        public void DisposeWorks()
        {
            using var client = DummyChannel.Instance.CreateGrpcService<IDisposableService>();
        }

        [Fact]
        public async Task DisposeAsyncWorks()
        {
            await using var client = DummyChannel.Instance.CreateGrpcService<IDisposableService>();
        }
    }

    internal sealed class DummyChannel : ChannelBase
    {
        public static DummyChannel Instance { get; } = new();
        private DummyChannel() : base("") { }
        public override CallInvoker CreateCallInvoker() => DummyCallInvoker.Instance;

        private sealed class DummyCallInvoker : CallInvoker
        {
            public static DummyCallInvoker Instance { get; } = new();
            private DummyCallInvoker() { }

            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
                => throw new NotSupportedException();

            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
                => throw new NotSupportedException();

            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
                => throw new NotSupportedException();

            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
                => throw new NotSupportedException();

            public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
                => throw new NotSupportedException();
        }
    }
}
