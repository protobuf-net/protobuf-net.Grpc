using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace protobuf_net.Grpc.Test
{
    class TestServerBinder : ServerBinder // just tracks what methods are observed
    {
        public HashSet<string> Methods { get; } = new HashSet<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
        protected override bool TryBind<TService, TRequest, TResponse>(ServiceBindContext bindContext, Method<TRequest, TResponse> method, MethodStub<TService> stub)
        {
            Methods.Add(method.FullName);
            return true;
        }
        protected internal override void OnWarn(string message, object?[]? args = null)
            => Warnings.Add(string.Format(message, args ?? Array.Empty<object>()));
        protected internal override void OnError(string message, object?[]? args = null)
            => Errors.Add(string.Format(message, args ?? Array.Empty<object>()));
    }

    class TestChannel : ChannelBase
    {
        public TestChannel(string target) : base(target) { }
        public override CallInvoker CreateCallInvoker()
            => new TestInvoker(this);

        public HashSet<string> Calls { get; } = new();

        private void Call(IMethod method)
            => Calls.Add(Target + ":" + method.FullName);

        class TestInvoker : CallInvoker
        {
            public TestChannel Channel { get; }
            public TestInvoker(TestChannel channel)
                => Channel = channel;

            public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
            {
                Channel.Call(method);
                return default!;
            }

            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
            {
                Channel.Call(method);
                return new AsyncUnaryCall<TResponse>(Task.FromResult<TResponse>(default!),
                    Task.FromResult(Metadata.Empty), () => Status.DefaultSuccess, () => Metadata.Empty, () => { });
            }

            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
            {
                Channel.Call(method);
                return new AsyncServerStreamingCall<TResponse>(NulStream<TResponse>.Default,
                    Task.FromResult(Metadata.Empty), () => Status.DefaultSuccess, () => Metadata.Empty, () => { });
            }

            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
            {
                Channel.Call(method);
                return new AsyncClientStreamingCall<TRequest, TResponse>(NulStream<TRequest>.Default, Task.FromResult<TResponse>(default!),
                    Task.FromResult(Metadata.Empty), () => Status.DefaultSuccess, () => Metadata.Empty, () => { });
            }

            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
            {
                Channel.Call(method);
                return new AsyncDuplexStreamingCall<TRequest, TResponse>(NulStream<TRequest>.Default, NulStream<TResponse>.Default,
                    Task.FromResult(Metadata.Empty), () => Status.DefaultSuccess, () => Metadata.Empty, () => { });
            }

            private sealed class NulStream<T> : IAsyncStreamReader<T>, IClientStreamWriter<T>
            {
                public static NulStream<T> Default { get; } = new();

                T IAsyncStreamReader<T>.Current => default!;

                WriteOptions IAsyncStreamWriter<T>.WriteOptions { get; set; } = WriteOptions.Default;

                private NulStream() { }

                Task<bool> IAsyncStreamReader<T>.MoveNext(CancellationToken cancellationToken)
                    => Task.FromResult(false);

                Task IAsyncStreamWriter<T>.WriteAsync(T message)
                    => Task.CompletedTask;

                Task IClientStreamWriter<T>.CompleteAsync()
                    => Task.CompletedTask;
            }
        }
    }
}
