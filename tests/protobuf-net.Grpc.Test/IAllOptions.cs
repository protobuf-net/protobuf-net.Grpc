using Grpc.Core;
using ProtoBuf;
using ProtoBuf.Grpc;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading.Tasks;

namespace protobuf_net.Grpc.Test
{
    [ProtoContract]
    public class HelloRequest
    {
        [ProtoMember(1)]
        public string? Name { get; set; }
    }
    [ProtoContract]
    public class HelloReply
    {
        [ProtoMember(1)]
        public string? Message { get; set; }
    }


    [ServiceContract]
    interface IAllOptions
    {
        // google client patterns
        HelloReply Client_BlockingUnary(HelloRequest request, CallOptions options);
        AsyncUnaryCall<HelloReply> Client_AsyncUnary(HelloRequest request, CallOptions options);
        AsyncClientStreamingCall<HelloRequest, HelloReply> Client_ClientStreaming(CallOptions options);
        AsyncDuplexStreamingCall<HelloRequest, HelloReply> Client_Duplex(CallOptions options);
        AsyncServerStreamingCall<HelloReply> Client_ServerStreaming(HelloRequest request, CallOptions options);

        // google server patterns
        Task<HelloReply> Server_Unary(HelloRequest request, ServerCallContext context);
        Task<HelloReply> Server_ClientStreaming(IAsyncStreamReader<HelloRequest> request, ServerCallContext context);
        Task Server_ServerStreaming(HelloRequest request, IServerStreamWriter<HelloReply> response, ServerCallContext context);
        Task Server_Duplex(IAsyncStreamReader<HelloRequest> request, IServerStreamWriter<HelloReply> response, ServerCallContext context);


        // blocking unary
        HelloReply Shared_BlockingUnary_NoContext(HelloRequest request);
        HelloReply Shared_BlockingUnary_Context(HelloRequest request, CallContext context);

        // async unary
        Task<HelloReply> Shared_TaskUnary_NoContext(HelloRequest request);
        Task<HelloReply> Shared_TaskUnary_Context(HelloRequest request, CallContext context);
        ValueTask<HelloReply> Shared_ValueTaskUnary_NoContext(HelloRequest request);
        ValueTask<HelloReply> Shared_ValueTaskUnary_Context(HelloRequest request, CallContext context);

        // client-streaming
        Task<HelloReply> Shared_TaskClientStreaming_NoContext(IAsyncEnumerable<HelloRequest> request);
        Task<HelloReply> Shared_TaskClientStreaming_Context(IAsyncEnumerable<HelloRequest> request, CallContext context);
        ValueTask<HelloReply> Shared_ValueTaskClientStreaming_NoContext(IAsyncEnumerable<HelloRequest> request);
        ValueTask<HelloReply> Shared_ValueTaskClientStreaming_Context(IAsyncEnumerable<HelloRequest> request, CallContext context);

        // server-streaming
        IAsyncEnumerable<HelloReply> Shared_ServerStreaming_NoContext(HelloRequest request);
        IAsyncEnumerable<HelloReply> Shared_ServerStreaming_Context(HelloRequest request, CallContext context);

        // duplex
        IAsyncEnumerable<HelloReply> Shared_Duplex_NoContext(IAsyncEnumerable<HelloRequest> request);
        IAsyncEnumerable<HelloReply> Shared_Duplex_Context(IAsyncEnumerable<HelloRequest> request, CallContext context);
    }
}
