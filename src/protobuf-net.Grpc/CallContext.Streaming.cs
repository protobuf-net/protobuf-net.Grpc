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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAsyncEnumerable<TResponse> FullDuplexAsync<TRequest, TResponse>(
            Func<CallContext, IAsyncEnumerable<TResponse>> producer,
            IAsyncEnumerable<TRequest> source,
            Func<IAsyncEnumerable<TRequest>, CallContext, ValueTask> consumer)
            => FullDuplexImpl<TRequest, TResponse>(this, producer, source, consumer, CancellationToken);

        private static async IAsyncEnumerable<TResponse> FullDuplexImpl<TRequest, TResponse>(
            CallContext context,
            Func<CallContext, IAsyncEnumerable<TResponse>> producer,
            IAsyncEnumerable<TRequest> source,
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAsyncEnumerable<TResponse> FullDuplexAsync<TRequest, TResponse>(
            Func<CallContext, IAsyncEnumerable<TResponse>> producer,
            IAsyncEnumerable<TRequest> source,
            Func<TRequest, CallContext, ValueTask> consumer)
            => FullDuplexImpl<TRequest, TResponse>(this, producer, source, consumer, CancellationToken);

        private static async IAsyncEnumerable<TResponse> FullDuplexImpl<TRequest, TResponse>(
            CallContext context,
            Func<CallContext, IAsyncEnumerable<TResponse>> producer,
            IAsyncEnumerable<TRequest> source,
            Func<TRequest, CallContext, ValueTask> consumer,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var consumed = Task.Run(async () => {// note this shares a capture scope
                await using var cIter = source.GetAsyncEnumerator(cancellationToken);
                while (await cIter.MoveNextAsync())
                {
                    await consumer(cIter.Current, context);
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
        /// Performs a full-duplex operation that will await the producer,
        /// performing a given opreation on each element from the input stream
        /// </summary>
        public ValueTask FullDuplexAsync<T>(
            Func<CallContext, ValueTask> producer,
            IAsyncEnumerable<T> source,
            Func<T, CallContext, ValueTask> consumer)
        {

            var context = this;
            var consumed = Task.Run(async () =>
            {   // note this shares a capture scope
                await using var cIter = source.GetAsyncEnumerator(context.CancellationToken);
                while (await cIter.MoveNextAsync())
                {
                    await consumer(cIter.Current, context);
                }
            }, context.CancellationToken);
            var produced = producer(context);
            if (produced.IsCompletedSuccessfully) return new ValueTask(consumed);
            return BothAsync(produced, consumed);
        }

        private static async ValueTask BothAsync(ValueTask produced, Task consumed)
        {
            try
            {
                await produced;
            }
            catch (Exception producerEx)
            {
                try
                {
                    await consumed; // make sure we try and await both
                }
                catch (Exception consumerEx)
                {
                    // so they *both* failed; talk about embarrassing!
                    throw new AggregateException(producerEx, consumerEx);
                }
                throw; // re-throw the exception from the producer
            }
            // producer completed cleanly; we can just await the
            // consumer - if it throws, it throws
            await consumed;
        }

        /// <summary>
        /// Performs a full-duplex operation that will await the producer and consumer stream
        /// </summary>
        public ValueTask FullDuplexAsync<T>(
            Func<CallContext, ValueTask> producer,
            IAsyncEnumerable<T> source,
            Func<IAsyncEnumerable<T>, CallContext, ValueTask> consumer)
        {
            var context = this;
            var consumed = Task.Run(() => consumer(source, context), context.CancellationToken); // note this shares a capture scope
            var produced = producer(context);
            if (produced.IsCompletedSuccessfully) return new ValueTask(consumed);
            return BothAsync(produced, consumed);
        }

        /// <summary>
        /// Performs an operation against each element in the inbound stream
        /// </summary>
        public async ValueTask ConsumeAsync<TRequest>(IAsyncEnumerable<TRequest> source, Func<TRequest, CallContext, ValueTask> consumer)
        {
            await using var iter = source.GetAsyncEnumerator(CancellationToken);
            while (await iter.MoveNextAsync())
            {
                await consumer(iter.Current, this);
            }
        }

        ///// <summary>
        ///// Performs an operation against each element in the inbound stream
        ///// </summary>
        //public async ValueTask ConsumeAsync<TRequest>(IAsyncEnumerable<TRequest> source, Func<TRequest, ValueTask> consumer)
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
        public async ValueTask<TValue> AggregateAsync<TRequest, TValue>(IAsyncEnumerable<TRequest> source,
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
        //public async ValueTask<TValue> AggregateAsync<TRequest, TValue>(IAsyncEnumerable<TRequest> source,
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
