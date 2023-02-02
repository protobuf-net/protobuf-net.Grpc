using Grpc.Core;
using ProtoBuf;
using ProtoBuf.Grpc;
using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Threading;
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
        HelloReply Shared_BlockingUnary_CancellationToken(HelloRequest request, CancellationToken cancellationToken);

        void Shared_BlockingUnary_NoContext_VoidVoid();
        void Shared_BlockingUnary_Context_VoidVoid(CallContext context);
        void Shared_BlockingUnary_CancellationToken_VoidVoid(CancellationToken cancellationToken);

        HelloReply Shared_BlockingUnary_NoContext_VoidVal();
        HelloReply Shared_BlockingUnary_Context_VoidVal(CallContext context);
        HelloReply Shared_BlockingUnary_CancellationToken_VoidVal(CancellationToken cancellationToken);

        void Shared_BlockingUnary_NoContext_ValVoid(HelloRequest request);
        void Shared_BlockingUnary_Context_ValVoid(HelloRequest request, CallContext context);
        void Shared_BlockingUnary_CancellationToken_ValVoid(HelloRequest request, CancellationToken cancellationToken);

        // async unary
        Task<HelloReply> Shared_TaskUnary_NoContext(HelloRequest request);
        Task<HelloReply> Shared_TaskUnary_Context(HelloRequest request, CallContext context);
        Task<HelloReply> Shared_TaskUnary_CancellationToken(HelloRequest request, CancellationToken cancellationToken);

        Task Shared_TaskUnary_NoContext_VoidVoid();
        Task Shared_TaskUnary_Context_VoidVoid(CallContext context);
        Task Shared_TaskUnary_CancellationToken_VoidVoid(CancellationToken cancellationToken);

        Task<HelloReply> Shared_TaskUnary_NoContext_VoidVal();
        Task<HelloReply> Shared_TaskUnary_Context_VoidVal(CallContext context);
        Task<HelloReply> Shared_TaskUnary_CancellationToken_VoidVal(CancellationToken cancellationToken);

        Task Shared_TaskUnary_NoContext_ValVoid(HelloRequest request);
        Task Shared_TaskUnary_Context_ValVoid(HelloRequest request, CallContext context);
        Task Shared_TaskUnary_CancellationToken_ValVoid(HelloRequest request, CancellationToken cancellationToken);

        ValueTask<HelloReply> Shared_ValueTaskUnary_NoContext(HelloRequest request);
        ValueTask<HelloReply> Shared_ValueTaskUnary_Context(HelloRequest request, CallContext context);
        ValueTask<HelloReply> Shared_ValueTaskUnary_CancellationToken(HelloRequest request, CancellationToken cancellationToken);

        ValueTask Shared_ValueTaskUnary_NoContext_VoidVoid();
        ValueTask Shared_ValueTaskUnary_Context_VoidVoid(CallContext context);
        ValueTask Shared_ValueTaskUnary_CancellationToken_VoidVoid(CancellationToken cancellationToken);

        ValueTask<HelloReply> Shared_ValueTaskUnary_NoContext_VoidVal();
        ValueTask<HelloReply> Shared_ValueTaskUnary_Context_VoidVal(CallContext context);
        ValueTask<HelloReply> Shared_ValueTaskUnary_CancellationToken_VoidVal(CancellationToken cancellationToken);

        ValueTask Shared_ValueTaskUnary_NoContext_ValVoid(HelloRequest request);
        ValueTask Shared_ValueTaskUnary_Context_ValVoid(HelloRequest request, CallContext context);
        ValueTask Shared_ValueTaskUnary_CancellationToken_ValVoid(HelloRequest request, CancellationToken cancellationToken);

        // client-streaming
        Task<HelloReply> Shared_TaskClientStreaming_NoContext(IAsyncEnumerable<HelloRequest> request);
        Task<HelloReply> Shared_TaskClientStreaming_Context(IAsyncEnumerable<HelloRequest> request, CallContext context);
        Task<HelloReply> Shared_TaskClientStreaming_CancellationToken(IAsyncEnumerable<HelloRequest> request, CancellationToken cancellationToken);
        Task Shared_TaskClientStreaming_NoContext_ValVoid(IAsyncEnumerable<HelloRequest> request);
        Task Shared_TaskClientStreaming_Context_ValVoid(IAsyncEnumerable<HelloRequest> request, CallContext context);
        Task Shared_TaskClientStreaming_CancellationToken_ValVoid(IAsyncEnumerable<HelloRequest> request, CancellationToken cancellationToken);

        ValueTask<HelloReply> Shared_ValueTaskClientStreaming_NoContext(IAsyncEnumerable<HelloRequest> request);
        ValueTask<HelloReply> Shared_ValueTaskClientStreaming_Context(IAsyncEnumerable<HelloRequest> request, CallContext context);
        ValueTask<HelloReply> Shared_ValueTaskClientStreaming_CancellationToken(IAsyncEnumerable<HelloRequest> request, CancellationToken cancellationToken);
        ValueTask Shared_ValueTaskClientStreaming_NoContext_ValVoid(IAsyncEnumerable<HelloRequest> request);
        ValueTask Shared_ValueTaskClientStreaming_Context_ValVoid(IAsyncEnumerable<HelloRequest> request, CallContext context);
        ValueTask Shared_ValueTaskClientStreaming_CancellationToken_ValVoid(IAsyncEnumerable<HelloRequest> request, CancellationToken cancellationToken);

        // server-streaming
        IAsyncEnumerable<HelloReply> Shared_ServerStreaming_NoContext(HelloRequest request);
        IAsyncEnumerable<HelloReply> Shared_ServerStreaming_Context(HelloRequest request, CallContext context);
        IAsyncEnumerable<HelloReply> Shared_ServerStreaming_CancellationToken(HelloRequest request, CancellationToken cancellationToken);
        IAsyncEnumerable<HelloReply> Shared_ServerStreaming_NoContext_VoidVal();
        IAsyncEnumerable<HelloReply> Shared_ServerStreaming_Context_VoidVal(CallContext context);
        IAsyncEnumerable<HelloReply> Shared_ServerStreaming_CancellationToken_VoidVal(CancellationToken cancellationToken);

        // duplex
        IAsyncEnumerable<HelloReply> Shared_Duplex_NoContext(IAsyncEnumerable<HelloRequest> request);
        IAsyncEnumerable<HelloReply> Shared_Duplex_Context(IAsyncEnumerable<HelloRequest> request, CallContext context);
        IAsyncEnumerable<HelloReply> Shared_Duplex_CancellationToken(IAsyncEnumerable<HelloRequest> request, CancellationToken cancellationToken);

        // client-streaming (observable)
        Task<HelloReply> Shared_TaskClientStreaming_NoContext_Observable(IObservable<HelloRequest> request);
        Task<HelloReply> Shared_TaskClientStreaming_Context_Observable(IObservable<HelloRequest> request, CallContext context);
        Task<HelloReply> Shared_TaskClientStreaming_CancellationToken_Observable(IObservable<HelloRequest> request, CancellationToken cancellationToken);
        Task Shared_TaskClientStreaming_NoContext_ValVoid_Observable(IObservable<HelloRequest> request);
        Task Shared_TaskClientStreaming_Context_ValVoid_Observable(IObservable<HelloRequest> request, CallContext context);
        Task Shared_TaskClientStreaming_CancellationToken_ValVoid_Observable(IObservable<HelloRequest> request, CancellationToken cancellationToken);

        ValueTask<HelloReply> Shared_ValueTaskClientStreaming_NoContext_Observable(IObservable<HelloRequest> request);
        ValueTask<HelloReply> Shared_ValueTaskClientStreaming_Context_Observable(IObservable<HelloRequest> request, CallContext context);
        ValueTask<HelloReply> Shared_ValueTaskClientStreaming_CancellationToken_Observable(IObservable<HelloRequest> request, CancellationToken cancellationToken);
        ValueTask Shared_ValueTaskClientStreaming_NoContext_ValVoid_Observable(IObservable<HelloRequest> request);
        ValueTask Shared_ValueTaskClientStreaming_Context_ValVoid_Observable(IObservable<HelloRequest> request, CallContext context);
        ValueTask Shared_ValueTaskClientStreaming_CancellationToken_ValVoid_Observable(IObservable<HelloRequest> request, CancellationToken cancellationToken);

        // server-streaming (observable)
        IObservable<HelloReply> Shared_ServerStreaming_NoContext_Observable(HelloRequest request);
        IObservable<HelloReply> Shared_ServerStreaming_Context_Observable(HelloRequest request, CallContext context);
        IObservable<HelloReply> Shared_ServerStreaming_CancellationToken_Observable(HelloRequest request, CancellationToken cancellationToken);
        IObservable<HelloReply> Shared_ServerStreaming_NoContext_VoidVal_Observable();
        IObservable<HelloReply> Shared_ServerStreaming_Context_VoidVal_Observable(CallContext context);
        IObservable<HelloReply> Shared_ServerStreaming_CancellationToken_VoidVal_Observable(CancellationToken cancellationToken);

        // duplex (observable)
        IObservable<HelloReply> Shared_Duplex_NoContext_Observable(IObservable<HelloRequest> request);
        IObservable<HelloReply> Shared_Duplex_Context_Observable(IObservable<HelloRequest> request, CallContext context);
        IObservable<HelloReply> Shared_Duplex_CancellationToken_Observable(IObservable<HelloRequest> request, CancellationToken cancellationToken);
    }
}
