using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using ProtoBuf.Grpc;

namespace protobuf_net.Grpc.Test
{
    public class AllOptionsService : IAllOptions
    {
        // shortcoming of compiler : simple implementation of IAsyncEnumerable<> uses the async keyword,
        // but then we get a CS1998 warning if nothing is awaited
#pragma warning disable 1998

        // Client call context meaningless on Server side
        public HelloReply Client_BlockingUnary(HelloRequest request, CallOptions options) => throw new NotSupportedException();

        public AsyncUnaryCall<HelloReply> Client_AsyncUnary(HelloRequest request, CallOptions options) => throw new NotSupportedException();

        public AsyncClientStreamingCall<HelloRequest, HelloReply> Client_ClientStreaming(CallOptions options) => throw new NotSupportedException();

        public AsyncDuplexStreamingCall<HelloRequest, HelloReply> Client_Duplex(CallOptions options) => throw new NotSupportedException();

        public AsyncServerStreamingCall<HelloReply> Client_ServerStreaming(HelloRequest request, CallOptions options) => throw new NotSupportedException();

        public Task<HelloReply> Server_Unary(HelloRequest request, ServerCallContext context) => Task.FromResult(new HelloReply { Message = $"Hello {request.Name}" });

        public async Task<HelloReply> Server_ClientStreaming(IAsyncStreamReader<HelloRequest> requestStream, ServerCallContext context)
        {
            bool hasNext = await requestStream.MoveNext();
            return new HelloReply { Message = $"Hello {(hasNext ? requestStream.Current.Name : "Anonymous Coward")}" };
        }

        public async Task Server_ServerStreaming(HelloRequest request, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
        {
            await responseStream.WriteAsync(new HelloReply { Message = $"Hello {request.Name}" });
        }

        public async Task Server_Duplex(IAsyncStreamReader<HelloRequest> requestStream, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
        {
            while (await requestStream.MoveNext())
                await responseStream.WriteAsync(new HelloReply { Message = $"Hello {requestStream.Current.Name}" });
        }

        public HelloReply Shared_BlockingUnary_NoContext(HelloRequest request) => new HelloReply { Message = $"Hello {request.Name}" };

        public HelloReply Shared_BlockingUnary_Context(HelloRequest request, CallContext context) => new HelloReply { Message = $"Hello {request.Name}" };

        public HelloReply Shared_BlockingUnary_CancellationToken(HelloRequest request, CancellationToken cancellationToken) => new HelloReply { Message = $"Hello {request.Name}" };

        public void Shared_BlockingUnary_NoContext_VoidVoid() { }

        public void Shared_BlockingUnary_Context_VoidVoid(CallContext context) { }

        public void Shared_BlockingUnary_CancellationToken_VoidVoid(CancellationToken cancellationToken) { }

        public HelloReply Shared_BlockingUnary_NoContext_VoidVal() => new HelloReply { Message = "Hello Anonymous Coward" };

        public HelloReply Shared_BlockingUnary_Context_VoidVal(CallContext context) => new HelloReply { Message = "Hello Anonymous Coward" };

        public HelloReply Shared_BlockingUnary_CancellationToken_VoidVal(CancellationToken cancellationToken) => new HelloReply { Message = "Hello Anonymous Coward" };

        public void Shared_BlockingUnary_NoContext_ValVoid(HelloRequest request) { }

        public void Shared_BlockingUnary_Context_ValVoid(HelloRequest request, CallContext context) { }

        public void Shared_BlockingUnary_CancellationToken_ValVoid(HelloRequest request, CancellationToken cancellationToken) { }

        public Task<HelloReply> Shared_TaskUnary_NoContext(HelloRequest request) => Task.FromResult(new HelloReply { Message = $"Hello {request.Name}" });

        public Task<HelloReply> Shared_TaskUnary_Context(HelloRequest request, CallContext context) => Task.FromResult(new HelloReply { Message = $"Hello {request.Name}" });

        public Task<HelloReply> Shared_TaskUnary_CancellationToken(HelloRequest request, CancellationToken cancellationToken) => Task.FromResult(new HelloReply { Message = $"Hello {request.Name}" });

        public Task Shared_TaskUnary_NoContext_VoidVoid() => Task.CompletedTask;

        public Task Shared_TaskUnary_Context_VoidVoid(CallContext context) => Task.CompletedTask;

        public Task Shared_TaskUnary_CancellationToken_VoidVoid(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<HelloReply> Shared_TaskUnary_NoContext_VoidVal() => Task.FromResult(new HelloReply { Message = "Hello Anonymous Coward" });

        public Task<HelloReply> Shared_TaskUnary_Context_VoidVal(CallContext context) => Task.FromResult(new HelloReply { Message = "Hello Anonymous Coward" });

        public Task<HelloReply> Shared_TaskUnary_CancellationToken_VoidVal(CancellationToken cancellationToken) => Task.FromResult(new HelloReply { Message = "Hello Anonymous Coward" });

        public Task Shared_TaskUnary_NoContext_ValVoid(HelloRequest request) => Task.CompletedTask;

        public Task Shared_TaskUnary_Context_ValVoid(HelloRequest request, CallContext context) => Task.CompletedTask;

        public Task Shared_TaskUnary_CancellationToken_ValVoid(HelloRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask<HelloReply> Shared_ValueTaskUnary_NoContext(HelloRequest request) => new ValueTask<HelloReply>(new HelloReply { Message = $"Hello {request.Name}" });

        public ValueTask<HelloReply> Shared_ValueTaskUnary_Context(HelloRequest request, CallContext context) => new ValueTask<HelloReply>(new HelloReply { Message = $"Hello {request.Name}" });

        public ValueTask<HelloReply> Shared_ValueTaskUnary_CancellationToken(HelloRequest request, CancellationToken cancellationToken) => new ValueTask<HelloReply>(new HelloReply { Message = $"Hello {request.Name}" });

        public ValueTask Shared_ValueTaskUnary_NoContext_VoidVoid() => new ValueTask();

        public ValueTask Shared_ValueTaskUnary_Context_VoidVoid(CallContext context) => new ValueTask();

        public ValueTask Shared_ValueTaskUnary_CancellationToken_VoidVoid(CancellationToken cancellationToken) => new ValueTask();

        public ValueTask<HelloReply> Shared_ValueTaskUnary_NoContext_VoidVal() => new ValueTask<HelloReply>(new HelloReply { Message = "Hello Anonymous Coward" });

        public ValueTask<HelloReply> Shared_ValueTaskUnary_Context_VoidVal(CallContext context) => new ValueTask<HelloReply>(new HelloReply { Message = "Hello Anonymous Coward" });

        public ValueTask<HelloReply> Shared_ValueTaskUnary_CancellationToken_VoidVal(CancellationToken cancellationToken) => new ValueTask<HelloReply>(new HelloReply { Message = "Hello Anonymous Coward" });

        public ValueTask Shared_ValueTaskUnary_NoContext_ValVoid(HelloRequest request) => new ValueTask();

        public ValueTask Shared_ValueTaskUnary_Context_ValVoid(HelloRequest request, CallContext context) => new ValueTask();

        public ValueTask Shared_ValueTaskUnary_CancellationToken_ValVoid(HelloRequest request, CancellationToken cancellationToken) => new ValueTask();

        public async Task<HelloReply> Shared_TaskClientStreaming_NoContext(IAsyncEnumerable<HelloRequest> requestStream)
        {
            HelloRequest? request = null;
            await foreach (var req in requestStream)
                request = req;
            return new HelloReply { Message = $"Hello {request?.Name ?? "Anonymous Coward"}" };
        }

        public async Task<HelloReply> Shared_TaskClientStreaming_Context(IAsyncEnumerable<HelloRequest> requestStream, CallContext context)
        {
            HelloRequest? request = null;
            await foreach (var req in requestStream)
                request = req;
            return new HelloReply { Message = $"Hello {request?.Name ?? "Anonymous Coward"}" };
        }

        public async Task<HelloReply> Shared_TaskClientStreaming_CancellationToken(IAsyncEnumerable<HelloRequest> requestStream, CancellationToken cancellationToken)
        {
            HelloRequest? request = null;
            await foreach (var req in requestStream)
                request = req;
            return new HelloReply { Message = $"Hello {request?.Name ?? "Anonymous Coward"}" };
        }

        public async Task Shared_TaskClientStreaming_NoContext_ValVoid(IAsyncEnumerable<HelloRequest> requestStream)
        {
            await foreach (var _ in requestStream)
            {
            }
        }

        public async Task Shared_TaskClientStreaming_Context_ValVoid(IAsyncEnumerable<HelloRequest> requestStream, CallContext context)
        {
            await foreach (var _ in requestStream)
            {
            }
        }

        public async Task Shared_TaskClientStreaming_CancellationToken_ValVoid(IAsyncEnumerable<HelloRequest> requestStream, CancellationToken cancellationToken)
        {
            await foreach (var _ in requestStream)
            {
            }
        }

        public async ValueTask<HelloReply> Shared_ValueTaskClientStreaming_NoContext(IAsyncEnumerable<HelloRequest> requestStream)
        {
            HelloRequest? request = null;
            await foreach (var req in requestStream)
                request = req;
            return new HelloReply { Message = $"Hello {request?.Name ?? "Anonymous Coward"}" };
        }

        public async ValueTask<HelloReply> Shared_ValueTaskClientStreaming_Context(IAsyncEnumerable<HelloRequest> requestStream, CallContext context)
        {
            HelloRequest? request = null;
            await foreach (var req in requestStream)
                request = req;
            return new HelloReply { Message = $"Hello {request?.Name ?? "Anonymous Coward"}" };
        }

        public async ValueTask<HelloReply> Shared_ValueTaskClientStreaming_CancellationToken(IAsyncEnumerable<HelloRequest> requestStream, CancellationToken cancellationToken)
        {
            HelloRequest? request = null;
            await foreach (var req in requestStream)
                request = req;
            return new HelloReply { Message = $"Hello {request?.Name ?? "Anonymous Coward"}" };
        }

        public async ValueTask Shared_ValueTaskClientStreaming_NoContext_ValVoid(IAsyncEnumerable<HelloRequest> requestStream)
        {
            await foreach (var _ in requestStream)
            {
            }
        }

        public async ValueTask Shared_ValueTaskClientStreaming_Context_ValVoid(IAsyncEnumerable<HelloRequest> requestStream, CallContext context)
        {
            await foreach (var _ in requestStream)
            {
            }
        }

        public async ValueTask Shared_ValueTaskClientStreaming_CancellationToken_ValVoid(IAsyncEnumerable<HelloRequest> requestStream, CancellationToken cancellationToken)
        {
            await foreach (var _ in requestStream)
            {
            }
        }

        public async IAsyncEnumerable<HelloReply> Shared_ServerStreaming_NoContext(HelloRequest request)
        {
            yield return new HelloReply { Message = $"Hello {request.Name}" };
        }

        public async IAsyncEnumerable<HelloReply> Shared_ServerStreaming_Context(HelloRequest request, CallContext context)
        {
            yield return new HelloReply { Message = $"Hello {request.Name}" };
        }

        public async IAsyncEnumerable<HelloReply> Shared_ServerStreaming_CancellationToken(HelloRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new HelloReply { Message = $"Hello {request.Name}" };
        }

        public async IAsyncEnumerable<HelloReply> Shared_ServerStreaming_NoContext_VoidVal()
        {
            yield return new HelloReply { Message = $"Hello Anonymous Coward" };
        }

        public async IAsyncEnumerable<HelloReply> Shared_ServerStreaming_Context_VoidVal(CallContext context)
        {
            yield return new HelloReply { Message = $"Hello Anonymous Coward" };
        }

        public async IAsyncEnumerable<HelloReply> Shared_ServerStreaming_CancellationToken_VoidVal([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new HelloReply { Message = $"Hello Anonymous Coward" };
        }

        public async IAsyncEnumerable<HelloReply> Shared_Duplex_NoContext(IAsyncEnumerable<HelloRequest> requestStream)
        {
            await foreach (var request in requestStream)
                yield return new HelloReply { Message = $"Hello {request.Name}" };
        }

        public async IAsyncEnumerable<HelloReply> Shared_Duplex_Context(IAsyncEnumerable<HelloRequest> requestStream, CallContext context)
        {
            await foreach (var request in requestStream)
                yield return new HelloReply { Message = $"Hello {request.Name}" };
        }

        public async IAsyncEnumerable<HelloReply> Shared_Duplex_CancellationToken(IAsyncEnumerable<HelloRequest> requestStream, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var request in requestStream)
                yield return new HelloReply { Message = $"Hello {request.Name}" };
        }
    }
}
