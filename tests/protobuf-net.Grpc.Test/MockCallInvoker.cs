using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Grpc.Core;

namespace protobuf_net.Grpc.Test
{
    public class MockCallInvoker : CallInvoker
    {
        private class MockServerCallContext : ServerCallContext
        {
            protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
            {
                return Task.CompletedTask;
            }

            protected override ContextPropagationToken? CreatePropagationTokenCore(ContextPropagationOptions options)
            {
                return null;
            }

            protected override string? MethodCore { get; }
            protected override string? HostCore { get; }
            protected override string? PeerCore { get; }
            protected override DateTime DeadlineCore { get; }
            protected override Metadata? RequestHeadersCore { get; }
            protected override CancellationToken CancellationTokenCore { get; }
            protected override Metadata? ResponseTrailersCore { get; }
            protected override Status StatusCore { get; set; }
            protected override WriteOptions? WriteOptionsCore { get; set; }
            protected override AuthContext? AuthContextCore { get; }
        }

        private class PassThroughStream<T> : IServerStreamWriter<T>, IAsyncStreamReader<T>, IClientStreamWriter<T>
        {
            private readonly Channel<T> _channel = System.Threading.Channels.Channel.CreateUnbounded<T>();

            public Task WriteAsync(T message)
            {
                return _channel.Writer.WriteAsync(message).AsTask();
            }

            public WriteOptions? WriteOptions { get; set; }

            public T Current { get; private set; } = default!;

            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                if (!await _channel.Reader.WaitToReadAsync(cancellationToken))
                    return false;

                Current = await _channel.Reader.ReadAsync(cancellationToken);
                return true;
            }

            public Task CompleteAsync()
            {
                _channel.Writer.Complete();
                return Task.CompletedTask;
            }
        }

        private readonly MockServiceBinder _serviceBinder;

        public MockCallInvoker(MockServiceBinder serviceBinder)
        {
            _serviceBinder = serviceBinder;
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
            string host, CallOptions options, TRequest request)
        {
            if (!_serviceBinder.Handlers.TryGetValue(method.FullName, out var handler))
                throw new InvalidOperationException($"Unknown method {method.FullName}");

            ServerCallContext context = new MockServerCallContext();
            var task = ((UnaryServerMethod<TRequest, TResponse>)handler).Invoke(request, context);
            return task.Result;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
        {
            if (!_serviceBinder.Handlers.TryGetValue(method.FullName, out var handler))
                throw new InvalidOperationException($"Unknown method {method.FullName}");

            ServerCallContext context = new MockServerCallContext();
            var task = ((UnaryServerMethod<TRequest, TResponse>)handler).Invoke(request, context);
            return new AsyncUnaryCall<TResponse>(task, Task.FromResult(new Metadata()),
                () => new Status(), () => new Metadata(), () => { });
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions options,
            TRequest request)
        {
            if (!_serviceBinder.Handlers.TryGetValue(method.FullName, out var handler))
                throw new InvalidOperationException($"Unknown method {method.FullName}");

            PassThroughStream<TResponse> stream = new PassThroughStream<TResponse>();
            ServerCallContext context = new MockServerCallContext();
            ((ServerStreamingServerMethod<TRequest, TResponse>)handler).Invoke(request, stream, context);
            return new AsyncServerStreamingCall<TResponse>(stream, Task.FromResult(new Metadata()),
                () => new Status(), () => new Metadata(), () => { });
        }

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions options)
        {
            if (!_serviceBinder.Handlers.TryGetValue(method.FullName, out var handler))
                throw new InvalidOperationException($"Unknown method {method.FullName}");

            PassThroughStream<TRequest> stream = new PassThroughStream<TRequest>();
            ServerCallContext context = new MockServerCallContext();
            var task = ((ClientStreamingServerMethod<TRequest, TResponse>)handler).Invoke(stream, context);
            return new AsyncClientStreamingCall<TRequest, TResponse>(stream, task,Task.FromResult(new Metadata()),
                () => new Status(), () => new Metadata(), () => { });
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method, string host, CallOptions options)
        {
            if (!_serviceBinder.Handlers.TryGetValue(method.FullName, out var handler))
                throw new InvalidOperationException($"Unknown method {method.FullName}");

            PassThroughStream<TRequest> requestStream = new PassThroughStream<TRequest>();
            PassThroughStream<TResponse> replyStream = new PassThroughStream<TResponse>();
            ServerCallContext context = new MockServerCallContext();
            ((DuplexStreamingServerMethod<TRequest, TResponse>)handler).Invoke(requestStream, replyStream, context);
            return new AsyncDuplexStreamingCall<TRequest, TResponse>(requestStream, replyStream,Task.FromResult(new Metadata()),
                () => new Status(), () => new Metadata(), () => { });
        }
    }
}
