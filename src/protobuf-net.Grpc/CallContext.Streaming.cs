using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc
{
    public readonly partial struct CallContext
    {
        /// <summary>
        /// Performs a full-duplex operation that will await both the producer and consumer streams
        /// </summary>
        public IAsyncEnumerable<TResponse> FullDuplexAsync<TRequest, TResponse>(
            IAsyncEnumerable<TRequest> source,
            Func<CallContext, IAsyncEnumerable<TResponse>> producer,
            Func<IAsyncEnumerable<TRequest>, CallContext, ValueTask> consumer)
            => FullDuplexImpl<TRequest, TResponse>(this, source, producer, consumer, CancellationToken);

        private static async IAsyncEnumerable<TResponse> FullDuplexImpl<TRequest, TResponse>(
            CallContext context,
            IAsyncEnumerable<TRequest> source,
            Func<CallContext, IAsyncEnumerable<TResponse>> producer,
            Func<IAsyncEnumerable<TRequest>, CallContext, ValueTask> consumer,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var consumed = Task.Run(() => consumer(source, context), cancellationToken); // note this shares a capture scope
            await using (var iter = producer(context).GetAsyncEnumerator(cancellationToken))
            {
                while (await iter.MoveNextAsync())
                {
                    yield return iter.Current;
                }
            }
            await consumed;
        }

        /// <summary>
        /// Performs a full-duplex operation that will await both the producer and consumer streams,
        /// performing a given opreation on each element from the input stream
        /// </summary>
        public IAsyncEnumerable<TResponse> FullDuplexAsync<TRequest, TResponse>(
            IAsyncEnumerable<TRequest> source,
            Func<CallContext, IAsyncEnumerable<TResponse>> producer,
            Func<TRequest, CallContext, ValueTask> consumer)
            => FullDuplexImpl<TRequest, TResponse>(this, source, producer, consumer, CancellationToken);

        private static async IAsyncEnumerable<TResponse> FullDuplexImpl<TRequest, TResponse>(
            CallContext context,
            IAsyncEnumerable<TRequest> source,
            Func<CallContext, IAsyncEnumerable<TResponse>> producer,
            Func<TRequest, CallContext, ValueTask> consumer,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var consumed = Task.Run(async () => {// note this shares a capture scope
                await using (var cIter = source.GetAsyncEnumerator(cancellationToken))
                {
                    while (await cIter.MoveNextAsync())
                    {
                        await consumer(cIter.Current, context);
                    }
                }
            }, cancellationToken);
            await using (var iter = producer(context).GetAsyncEnumerator(cancellationToken))
            {
                while (await iter.MoveNextAsync())
                {
                    yield return iter.Current;
                }
            }
            await consumed;
        }

        /// <summary>
        /// Performs an operation against each element in the inbound stream
        /// </summary>
        public async Task ConsumeAsync<TRequest>(IAsyncEnumerable<TRequest> source, Func<TRequest, CallContext, ValueTask> consumer)
        {
            await using (var iter = source.GetAsyncEnumerator(CancellationToken))
            {
                while (await iter.MoveNextAsync())
                {
                    await consumer(iter.Current, this);
                }
            }
        }

        ///// <summary>
        ///// Performs an operation against each element in the inbound stream
        ///// </summary>
        //public async Task ConsumeAsync<TRequest>(IAsyncEnumerable<TRequest> source, Func<TRequest, ValueTask> consumer)
        //{
        //    await using (var iter = source.GetAsyncEnumerator(CancellationToken))
        //    {
        //        while (await iter.MoveNextAsync())
        //        {
        //            await consumer(iter.Current);
        //        }
        //    }
        //}

        /// <summary>
        /// Performs an aggregate operation against each element in the inbound stream
        /// </summary>
        public async Task<TValue> AggregateAsync<TRequest, TValue>(IAsyncEnumerable<TRequest> source,
            Func<TValue, TRequest, CallContext, ValueTask<TValue>> aggregate, TValue seed)
        {
            await using (var iter = source.GetAsyncEnumerator(CancellationToken))
            {
                while (await iter.MoveNextAsync())
                {
                    seed = await aggregate(seed, iter.Current, this);
                }
            }
            return seed;
        }

        ///// <summary>
        ///// Performs an aggregate operation against each element in the inbound stream
        ///// </summary>
        //public async Task<TValue> AggregateAsync<TRequest, TValue>(IAsyncEnumerable<TRequest> source,
        //    Func<TValue, TRequest, ValueTask<TValue>> aggregate, TValue seed)
        //{
        //    await using (var iter = source.GetAsyncEnumerator(CancellationToken))
        //    {
        //        while (await iter.MoveNextAsync())
        //        {
        //            seed = await aggregate(seed, iter.Current);
        //        }
        //    }
        //    return seed;
        //}
    }
}
