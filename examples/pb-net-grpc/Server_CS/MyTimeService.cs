using MegaCorp;
using ProtoBuf.Grpc;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Server_CS
{
    public class MyTimeService : ITimeService
    {
        public IAsyncEnumerable<TimeResult> SubscribeAsync(CallContext context = default)
            => SubscribeAsyncImpl(context.CancellationToken);

        private static async IAsyncEnumerable<TimeResult> SubscribeAsyncImpl([EnumeratorCancellation] CancellationToken cancel)
        {
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancel);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                yield return new TimeResult { Time = DateTime.UtcNow, Id = Guid.NewGuid() };
            }
        }
    }
}
