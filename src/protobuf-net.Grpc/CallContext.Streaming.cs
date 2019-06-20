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
            Func<CallContext, CancellationToken, IAsyncEnumerable<TResponse>> producer,
            Func<IAsyncEnumerable<TRequest>, CallContext, Task> consumer)
        {
            AssertServer();
            return FullDuplexImpl(ConsumeAsWorkerAsync(source, consumer), producer, CancellationToken);
        }

        /// <summary>
        /// Performs a full-duplex operation that will await both the producer and consumer streams
        /// </summary>
        public IAsyncEnumerable<TResponse> FullDuplexAsync<TRequest, TResponse>(
            IAsyncEnumerable<TRequest> source,
            Func<CallContext, IAsyncEnumerable<TResponse>> producer,
            Func<IAsyncEnumerable<TRequest>, CallContext, Task> consumer)
        {
            AssertServer();
            return FullDuplexImpl(ConsumeAsWorkerAsync(source, consumer), producer, CancellationToken);
        }

        /// <summary>
        /// Performs a full-duplex operation that will await both the producer and consumer streams,
        /// performing an operation against each element in the inbound stream
        /// </summary>
        public IAsyncEnumerable<TResponse> FullDuplexAsync<TRequest, TResponse>(
            IAsyncEnumerable<TRequest> source,
            Func<CallContext, CancellationToken, IAsyncEnumerable<TResponse>> producer,
            Func<TRequest, CallContext, ValueTask> consumer)
        {
            AssertServer();
            return  FullDuplexImpl(ConsumeAsWorkerAsync(source, consumer), producer, CancellationToken);
        }

        /// <summary>
        /// Performs a full-duplex operation that will await both the producer and consumer streams,
        /// performing an operation against each element in the inbound stream
        /// </summary>
        public IAsyncEnumerable<TResponse> FullDuplexAsync<TRequest, TResponse>(
            IAsyncEnumerable<TRequest> source,
            Func<CallContext, IAsyncEnumerable<TResponse>> producer,
            Func<TRequest, CallContext, ValueTask> consumer)
        {
            AssertServer();
            return FullDuplexImpl(ConsumeAsWorkerAsync(source, consumer), producer, CancellationToken);
        }

        /// <summary>
        /// Performs a full-duplex operation that will await both the producer and consumer streams,
        /// performing an operation against each element in the inbound stream
        /// </summary>
        public IAsyncEnumerable<TResponse> FullDuplexAsync<TServer, TRequest, TResponse>(
            IAsyncEnumerable<TRequest> source,
            Func<TServer, CallContext, IAsyncEnumerable<TResponse>> producer,
            Func<TServer, TRequest, CallContext, ValueTask> consumer)
            where TServer : class
        {
            TServer server = GetServer<TServer>();
            return FullDuplexImpl(server, ConsumeAsWorkerAsync(server, source, consumer), producer, CancellationToken);
        }

        /// <summary>
        /// Performs a full-duplex operation that will await both the producer and consumer streams
        /// </summary>
        public IAsyncEnumerable<TResponse> FullDuplexAsync<TServer, TRequest, TResponse>(
            IAsyncEnumerable<TRequest> source,
            Func<TServer, CallContext, IAsyncEnumerable<TResponse>> producer,
            Func<TServer, IAsyncEnumerable<TRequest>, CallContext, Task> consumer)
            where TServer : class
        {
            TServer server = GetServer<TServer>();
            return FullDuplexImpl(server, ConsumeAsWorkerAsync(server, source, consumer), producer, CancellationToken);
        }

        /// <summary>
        /// Performs a full-duplex operation that will await both the producer and consumer streams
        /// </summary>
        public IAsyncEnumerable<TResponse> FullDuplexAsync<TServer, TRequest, TResponse>(
            IAsyncEnumerable<TRequest> source,
            Func<TServer, CallContext, CancellationToken, IAsyncEnumerable<TResponse>> producer,
            Func<TServer, IAsyncEnumerable<TRequest>, CallContext, Task> consumer)
            where TServer : class
        {
            TServer server = GetServer<TServer>();
            return FullDuplexImpl(server, ConsumeAsWorkerAsync(server, source, consumer), producer, CancellationToken);
        }

        /// <summary>
        /// Performs an operation against each element in the inbound stream
        /// </summary>
        public async Task ClientStreamingAsync<TRequest>(IAsyncEnumerable<TRequest> source, Func<TRequest, CallContext, ValueTask> consumer)
        {
            await using (var iter = source.GetAsyncEnumerator(CancellationToken))
            {
                while (await iter.MoveNextAsync())
                {
                    await consumer(iter.Current, this);
                }
            }
        }

        /// <summary>
        /// Performs an aggregate operation against each element in the inbound stream
        /// </summary>
        public async Task<TValue> ClientStreamingAsync<TRequest, TValue>(IAsyncEnumerable<TRequest> source,
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

        /// <summary>
        /// Performs an operation against each element in the inbound stream
        /// </summary>
        public async Task ClientStreamingAsync<TServer, TRequest>(TServer server, IAsyncEnumerable<TRequest> source,
            Func<TServer, TRequest, CallContext, ValueTask> consumer)
        {
            await using (var iter = source.GetAsyncEnumerator(CancellationToken))
            {
                while (await iter.MoveNextAsync())
                {
                    await consumer(server, iter.Current, this);
                }
            }
        }

        /// <summary>
        /// Performs an aggregate operation against each element in the inbound stream
        /// </summary>
        public async Task<TValue> ClientStreamingAsync<TServer, TRequest, TValue>(TServer server, IAsyncEnumerable<TRequest> source,
            Func<TServer, TValue, TRequest, CallContext, ValueTask<TValue>> aggregate, TValue seed)
        {
            await using (var iter = source.GetAsyncEnumerator(CancellationToken))
            {
                while (await iter.MoveNextAsync())
                {
                    await aggregate(server, seed, iter.Current, this);
                }
            }
            return seed;
        }

        private async IAsyncEnumerable<TResponse> FullDuplexImpl<TResponse>(
            Task consumed,
            Func<CallContext, IAsyncEnumerable<TResponse>> producer,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await using (var iter = producer(this).GetAsyncEnumerator(cancellationToken))
            {
                while (await iter.MoveNextAsync())
                {
                    yield return iter.Current;
                }
            }
            await consumed;
        }

        private async IAsyncEnumerable<TResponse> FullDuplexImpl<TResponse>(
            Task consumed,
            Func<CallContext, CancellationToken, IAsyncEnumerable<TResponse>> producer,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await using (var iter = producer(this, this.CancellationToken).GetAsyncEnumerator(cancellationToken))
            {
                while (await iter.MoveNextAsync())
                {
                    yield return iter.Current;
                }
            }
            await consumed;
        }

        private async IAsyncEnumerable<TResponse> FullDuplexImpl<TServer, TResponse>(
            TServer server, Task consumed,
            Func<TServer, CallContext, IAsyncEnumerable<TResponse>> producer,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await using (var iter = producer(server, this).GetAsyncEnumerator(cancellationToken))
            {
                while (await iter.MoveNextAsync())
                {
                    yield return iter.Current;
                }
            }
            await consumed;
        }

        private async IAsyncEnumerable<TResponse> FullDuplexImpl<TServer, TResponse>(
            TServer server, Task consumed,
            Func<TServer, CallContext, CancellationToken, IAsyncEnumerable<TResponse>> producer,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await using (var iter = producer(server, this, this.CancellationToken).GetAsyncEnumerator(cancellationToken))
            {
                while (await iter.MoveNextAsync())
                {
                    yield return iter.Current;
                }
            }
            await consumed;
        }

        private Task ConsumeAsWorkerAsync<TRequest>(IAsyncEnumerable<TRequest> source, Func<IAsyncEnumerable<TRequest>, CallContext, Task> consumer)
        {
            var ctx = this;
            return Task.Run(() => consumer(source, ctx), CancellationToken);
        }

        private Task ConsumeAsWorkerAsync<TServer, TRequest>(TServer server, IAsyncEnumerable<TRequest> source, Func<TServer, IAsyncEnumerable<TRequest>, CallContext, Task> consumer)
        {
            var ctx = this;
            return Task.Run(() => consumer(server, source, ctx), CancellationToken);
        }

        private Task ConsumeAsWorkerAsync<TRequest>(IAsyncEnumerable<TRequest> source, Func<TRequest, CallContext, ValueTask> consumer)
        {
            var ctx = this;
            return Task.Run(() => ctx.ClientStreamingAsync<TRequest>(source, consumer), CancellationToken);
        }

        private Task ConsumeAsWorkerAsync<TServer, TRequest>(TServer server, IAsyncEnumerable<TRequest> source, Func<TServer, TRequest, CallContext, ValueTask> consumer)
        {
            var ctx = this;
            return Task.Run(() => ctx.ClientStreamingAsync<TServer, TRequest>(server, source, consumer), CancellationToken);
        }
    }
}
