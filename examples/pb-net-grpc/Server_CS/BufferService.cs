using Shared_CS;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server_CS
{
    public class BufferService : IBufferScenarios
    {
        async IAsyncEnumerable<SimpleBuffer> IBufferScenarios.Simple(IAsyncEnumerable<SimpleBuffer> source)
        {
            // yes we could reverse in-place, but I want to assume more complex where the data is isolated and distinct
            await foreach (var inbound in source)
            {
                var clone = inbound.Data.ToArray();
                clone.Reverse();
                yield return new SimpleBuffer { Data = clone };
            }
        }

        async IAsyncEnumerable<AdvancedBuffer> IBufferScenarios.Advanced(IAsyncEnumerable<AdvancedBuffer> source)
        {
            // yes we could reverse in-place, but I want to assume more complex where the data is isolated and distinct
            await foreach (var inbound in source)
            {
                AdvancedBuffer outbound = new(inbound.Length);
                inbound.Span.CopyTo(outbound.Span);
                outbound.Span.Reverse();
                inbound.Dispose(); // handle lifetime of inbound
                yield return outbound; // lifetime of outbound managed by the marshaller
            }
        }
    }
}
