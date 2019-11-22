using System.Threading;
using System.Threading.Tasks;

namespace protobuf_net.Grpc.Test
{
    public class LegacyService : ILegacyService
    {
        public HelloReply Shared_Legacy_BlockingUnary(string arg1, long arg2) =>
            new HelloReply { Message = $"Hello {arg1}, aged {arg2}" };

        public HelloReply Shared_Legacy_BlockingUnary_ManyArgs(string arg1, long arg2, HelloRequest arg3, string arg4, long arg5,
            HelloRequest arg6, string arg7, long arg8, HelloRequest arg9) =>
            new HelloReply { Message = $"Hello {arg1}, aged {arg2}" };

        public void Shared_Legacy_BlockingUnary_ValVoid(string arg1, long arg2) { }

        public Task<HelloReply> Shared_Legacy_TaskUnary(string arg1, long arg2) =>
            Task.FromResult(new HelloReply { Message = $"Hello {arg1}, aged {arg2}" });

        public Task Shared_Legacy_TaskUnary_ValVoid(string arg1, long arg2) => Task.CompletedTask;

        public ValueTask<HelloReply> Shared_Legacy_ValueTaskUnary(string arg1, long arg2) =>
            new ValueTask<HelloReply>(new HelloReply { Message = $"Hello {arg1}, aged {arg2}" });

        public ValueTask Shared_Legacy_ValueTaskUnary_ValVoid(string arg1, long arg2) => new ValueTask();

        public Task<HelloReply> Shared_Legacy_TaskUnary_CancellationToken(string arg1, long arg2, CancellationToken cancellationToken) =>
            Task.FromResult(new HelloReply { Message = $"Hello {arg1}, aged {arg2}" });

        public Task Shared_Legacy_TaskUnary_CancellationToken_ValVoid(string arg1, long arg2, CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask<HelloReply> Shared_Legacy_ValueTaskUnary_CancellationToken(string arg1, long arg2, CancellationToken cancellationToken) =>
            new ValueTask<HelloReply>(new HelloReply { Message = $"Hello {arg1}, aged {arg2}" });

        public ValueTask Shared_Legacy_ValueTaskUnary_CancellationToken_ValVoid(string arg1, long arg2, CancellationToken cancellationToken) => new ValueTask();
    }
}
