using MegaCorp;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable CS0618
namespace Server_CS
{
    public class MyTimeService : ITimeService
    {
        public IAsyncEnumerable<TimeResult> SubscribeAsync(Empty empty, CallContext context = default)
            => SubscribeAsync(default); // context.CancellationToken);

        private async IAsyncEnumerable<TimeResult> SubscribeAsync([EnumeratorCancellation] CancellationToken cancel)
        {
            while (!cancel.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                yield return new TimeResult { Time = DateTime.UtcNow };
            }
        }
    }
}
