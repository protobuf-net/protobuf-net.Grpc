using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using PerfTest;

namespace PerfServer
{
    public sealed class VanillaGrpcServer : VanillaGrpc.VanillaGrpcBase
    {
        private static int _count;
        private static readonly Task<Empty> s_empty = Task.FromResult(new Empty { });
        public override Task<Empty> Increment(Empty request, ServerCallContext context)
        {
            Interlocked.Increment(ref _count);
            return s_empty;
        }
        public override Task<IncrementResult> Reset(Empty request, ServerCallContext context)
        {
            return Task.FromResult(new IncrementResult { Count = Interlocked.Exchange(ref _count, 0) });
        }
    }
}
