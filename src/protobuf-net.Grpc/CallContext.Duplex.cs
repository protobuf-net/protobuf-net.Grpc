using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc
{
    public readonly partial struct CallContext
    {
        public IAsyncEnumerable<TResponse> FullDuplex<TRequest, TResponse>(
            IAsyncEnumerable<TRequest> source,
            Func<CallContext, CancellationToken, IAsyncEnumerable<TResponse>> producer,
            Func<IAsyncEnumerable<TRequest>, CallContext, Task> consumer)
        {
            AssertServer();
            return FullDuplexImpl(ConsumeAsync(source, consumer), producer, CancellationToken);
        }

        public IAsyncEnumerable<TResponse> FullDuplex<TRequest, TResponse>(
            IAsyncEnumerable<TRequest> source,
            Func<CallContext, IAsyncEnumerable<TResponse>> producer,
            Func<IAsyncEnumerable<TRequest>, CallContext, Task> consumer)
        {
            AssertServer();
            return FullDuplexImpl(ConsumeAsync(source, consumer), producer, CancellationToken);
        }

        public IAsyncEnumerable<TResponse> FullDuplex<TRequest, TResponse>(
            IAsyncEnumerable<TRequest> source,
            Func<CallContext, CancellationToken, IAsyncEnumerable<TResponse>> producer,
            Func<TRequest, CallContext, ValueTask> consumer)
        {
            AssertServer();
            return  FullDuplexImpl(ConsumeAsync(source, consumer), producer, CancellationToken);
        }

        public IAsyncEnumerable<TResponse> FullDuplex<TRequest, TResponse>(
            IAsyncEnumerable<TRequest> source,
            Func<CallContext, IAsyncEnumerable<TResponse>> producer,
            Func<TRequest, CallContext, ValueTask> consumer)
        {
            AssertServer();
            return FullDuplexImpl(ConsumeAsync(source, consumer), producer, CancellationToken);
        }

        public IAsyncEnumerable<TResponse> FullDuplex<TServer, TRequest, TResponse>(
            IAsyncEnumerable<TRequest> source,
            Func<TServer, CallContext, IAsyncEnumerable<TResponse>> producer,
            Func<TServer, TRequest, CallContext, ValueTask> consumer)
            where TServer : class
        {
            TServer server = GetServer<TServer>();
            return FullDuplexImpl(server, ConsumeAsync(server, source, consumer), producer, CancellationToken);
        }

        public IAsyncEnumerable<TResponse> FullDuplex<TServer, TRequest, TResponse>(
            IAsyncEnumerable<TRequest> source,
            Func<TServer, CallContext, IAsyncEnumerable<TResponse>> producer,
            Func<TServer, IAsyncEnumerable<TRequest>, CallContext, Task> consumer)
            where TServer : class
        {
            TServer server = GetServer<TServer>();
            return FullDuplexImpl(server, ConsumeAsync(server, source, consumer), producer, CancellationToken);
        }

        public IAsyncEnumerable<TResponse> FullDuplex<TServer, TRequest, TResponse>(
            IAsyncEnumerable<TRequest> source,
            Func<TServer, CallContext, CancellationToken, IAsyncEnumerable<TResponse>> producer,
            Func<TServer, IAsyncEnumerable<TRequest>, CallContext, Task> consumer)
            where TServer : class
        {
            TServer server = GetServer<TServer>();
            return FullDuplexImpl(server, ConsumeAsync(server, source, consumer), producer, CancellationToken);
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

        private Task ConsumeAsync<TRequest>(IAsyncEnumerable<TRequest> source, Func<IAsyncEnumerable<TRequest>, CallContext, Task> consumer)
        {
            var ctx = this;
            return Task.Run(() => consumer(source, ctx));
        }

        private Task ConsumeAsync<TServer, TRequest>(TServer server, IAsyncEnumerable<TRequest> source, Func<TServer, IAsyncEnumerable<TRequest>, CallContext, Task> consumer)
        {
            var ctx = this;
            return Task.Run(() => consumer(server, source, ctx));
        }

        private Task ConsumeAsync<TRequest>(IAsyncEnumerable<TRequest> source, Func<TRequest, CallContext, ValueTask> consumer)
        {
            var ctx = this;
            return Task.Run(() => ConsumeAsyncImpl(ctx, source, consumer));

            static async Task ConsumeAsyncImpl(CallContext context, IAsyncEnumerable<TRequest> source, Func<TRequest, CallContext, ValueTask> consumer)
            {
                await using (var iter = source.GetAsyncEnumerator(context.CancellationToken))
                {
                    while (await iter.MoveNextAsync())
                    {
                        await consumer(iter.Current, context);
                    }
                }
            }
        }

        private Task ConsumeAsync<TServer, TRequest>(TServer server, IAsyncEnumerable<TRequest> source, Func<TServer, TRequest, CallContext, ValueTask> consumer)
        {
            var ctx = this;
            return Task.Run(() => ConsumeAsyncImpl(server, ctx, source, consumer));

            static async Task ConsumeAsyncImpl(TServer server, CallContext context, IAsyncEnumerable<TRequest> source, Func<TServer, TRequest, CallContext, ValueTask> consumer)
            {
                await using (var iter = source.GetAsyncEnumerator(context.CancellationToken))
                {
                    while (await iter.MoveNextAsync())
                    {
                        await consumer(server, iter.Current, context);
                    }
                }
            }
        }
    }
}
