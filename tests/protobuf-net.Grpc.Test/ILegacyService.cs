using System;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace protobuf_net.Grpc.Test
{
    /// <summary>
    /// Legacy ServiceContract, not directly in the gRPC Request/Reply model
    /// </summary>
    [ServiceContract]
    interface ILegacyService
    {
        // blocking, multiple arguments
        HelloReply Shared_Legacy_BlockingUnary(string arg1, long arg2);
        HelloReply Shared_Legacy_BlockingUnary_ManyArgs(string arg1, long arg2, HelloRequest arg3,
            string arg4, long arg5, HelloRequest arg6,string arg7, long arg8, HelloRequest arg9);
        void Shared_Legacy_BlockingUnary_ValVoid(string arg1, long arg2);

        // async, multiple arguments
        Task<HelloReply> Shared_Legacy_TaskUnary(string arg1, long arg2);
        Task Shared_Legacy_TaskUnary_ValVoid(string arg1, long arg2);
        ValueTask<HelloReply> Shared_Legacy_ValueTaskUnary(string arg1, long arg2);
        ValueTask Shared_Legacy_ValueTaskUnary_ValVoid(string arg1, long arg2);

        // async, multiple arguments and a CancellationToken
        Task<HelloReply> Shared_Legacy_TaskUnary_CancellationToken(string arg1, long arg2, CancellationToken cancellationToken);
        Task Shared_Legacy_TaskUnary_CancellationToken_ValVoid(string arg1, long arg2, CancellationToken cancellationToken);
        ValueTask<HelloReply> Shared_Legacy_ValueTaskUnary_CancellationToken(string arg1, long arg2, CancellationToken cancellationToken);
        ValueTask Shared_Legacy_ValueTaskUnary_CancellationToken_ValVoid(string arg1, long arg2, CancellationToken cancellationToken);
    }
}
