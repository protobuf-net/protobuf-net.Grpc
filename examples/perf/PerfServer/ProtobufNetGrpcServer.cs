using System.Threading;
using System.Threading.Tasks;
using CodeFirst;

namespace PerfServer
{
    public sealed class ProtobufNetGrpcServer : IProtobufNetGrpc
    {
        private static int _count;

        public Task Increment()
        {
            Interlocked.Increment(ref _count);
            return Task.CompletedTask;
        }

        public Task<IncrementResult> Reset()
        {
            return Task.FromResult(new IncrementResult { Count = Interlocked.Exchange(ref _count, 0) });
        }
    }
}
