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
            using var allDone = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
            try
            {
                context = new CallContext(context, allDone.Token);
                var consumed = Task.Run(() => consumer(source, context), allDone.Token); // note this shares a capture scope

                await foreach (var value in producer(context).WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    yield return value;
                }

                await consumed.ConfigureAwait(false);
            }
            finally
            {
                // stop the producer, in any exit scenario
                allDone.Cancel();
            }
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
            using var allDone = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
            try
            {
                context = new CallContext(context, allDone.Token);
                var consumed = Task.Run(async () =>
                {// note this shares a capture scope
                    await foreach (var value in source.WithCancellation(allDone.Token).ConfigureAwait(false))
                    {
                        await consumer(value, context).ConfigureAwait(false);
                    }
                }, allDone.Token);
                await foreach (var value in producer(context).WithCancellation(allDone.Token).ConfigureAwait(false))
                {
                    yield return value;
                }
                await consumed.ConfigureAwait(false);
            }
            finally
            {
                // stop the producer, in any exit scenario
                allDone.Cancel();
            }
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
            using var allDone = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, default);
            try
            {
                var context = new CallContext(this, allDone.Token);
                var consumed = Task.Run(async () =>
                {   // note this shares a capture scope
                    await foreach (var value in source.WithCancellation(context.CancellationToken).ConfigureAwait(false))
                    {
                        await consumer(value, context).ConfigureAwait(false);
                    }
                }, context.CancellationToken);
                var produced = producer(context);
                if (produced.IsCompletedSuccessfully) return new ValueTask(consumed);
                return BothAsync(produced, consumed);
            }
            finally
            {
                // stop the producer, in any exit scenario
                allDone.Cancel();
            }
        }

        private static async ValueTask BothAsync(ValueTask produced, Task consumed)
        {
            try
            {
                await produced.ConfigureAwait(false);
            }
            catch (Exception producerEx)
            {
                try
                {
                    await consumed.ConfigureAwait(false); // make sure we try and await both
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
            await consumed.ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a full-duplex operation that will await the producer and consumer stream
        /// </summary>
        public ValueTask FullDuplexAsync<T>(
            Func<CallContext, ValueTask> producer,
            IAsyncEnumerable<T> source,
            Func<IAsyncEnumerable<T>, CallContext, ValueTask> consumer)
        {
            using var allDone = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, default);
            try
            {
                var context = new CallContext(this, allDone.Token);
                var consumed = Task.Run(() => consumer(source, context), context.CancellationToken); // note this shares a capture scope
                var produced = producer(context);
                if (produced.IsCompletedSuccessfully) return new ValueTask(consumed);
                return BothAsync(produced, consumed);
            }
            finally
            {
                // stop the producer, in any exit scenario
                allDone.Cancel();
            }
        }

        /// <summary>
        /// Performs an operation against each element in the inbound stream
        /// </summary>
        public async ValueTask ConsumeAsync<TRequest>(IAsyncEnumerable<TRequest> source, Func<TRequest, CallContext, ValueTask> consumer)
        {
            await foreach (var value in source.WithCancellation(CancellationToken).ConfigureAwait(false))
            {
                await consumer(value, this).ConfigureAwait(false);
            }
        }

        ///// <summary>
        ///// Performs an operation against each element in the inbound stream
        ///// </summary>
        //public async ValueTask ConsumeAsync<TRequest>(IAsyncEnumerable<TRequest> source, Func<TRequest, ValueTask> consumer)
        //{
        //    await foreach (var value in source.WithCancellation(CancellationToken).ConfigureAwait(false))
        //    {
        //        await consumer(value).ConfigureAwait(false);
        //    }
        //}

        /// <summary>
        /// Performs an aggregate operation against each element in the inbound stream
        /// </summary>
        public async ValueTask<TValue> AggregateAsync<TRequest, TValue>(IAsyncEnumerable<TRequest> source,
            Func<TValue, TRequest, CallContext, ValueTask<TValue>> aggregate, TValue seed)
        {
            await foreach (var value in source.WithCancellation(CancellationToken).ConfigureAwait(false))
            {
                seed = await aggregate(seed, value, this).ConfigureAwait(false);
            }
            return seed;
        }

        ///// <summary>
        ///// Performs an aggregate operation against each element in the inbound stream
        ///// </summary>
        //public async ValueTask<TValue> AggregateAsync<TRequest, TValue>(IAsyncEnumerable<TRequest> source,
        //    Func<TValue, TRequest, ValueTask<TValue>> aggregate, TValue seed)
        //{
        //    await foreach (var value in source.WithCancellation(CancellationToken).ConfigureAwait(false))
        //    {
        //        seed = await aggregate(seed, iter.Current).ConfigureAwait(false);
        //    }
        //    return seed;
        //}
    }
}
