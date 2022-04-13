using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace ProtoBuf.Grpc.Internal
{
    /// <summary>
    /// Provides APIs to shim between the traditional gRPC API and an idiomatic .NET API
    /// </summary>
    [Obsolete(WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public static class Reshape
    {
        internal const string WarningMessage = "This API is intended for use by runtime-generated code; all types and methods can be changed without notice - it is only guaranteed to work with the internally generated code";

        /// <summary>
        /// Provides a task that is equivalent to a void operation
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<Empty> EmptyValueTask(ValueTask task)
        {
            if (task.IsCompletedSuccessfully) return Empty.InstanceTask;

            return Awaited(task);
            static async Task<Empty> Awaited(ValueTask t)
            {
                await t.ConfigureAwait(false);
                return Empty.Instance;
            }
        }

        /// <summary>
        /// Provides a task that is equivalent to a void operation
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<Empty> EmptyTask(Task task)
        {
            if (task.RanToCompletion()) return Empty.InstanceTask;

            return Awaited(task);
            static async Task<Empty> Awaited(Task t)
            {
                await t.ConfigureAwait(false);
                return Empty.Instance;
            }
        }

        /* experimental; if we wanted to support server-side implementations using the client-side API? or is that just daft?

        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static async Task<T> WriteTo<T>(AsyncUnaryCall<T> result, ServerCallContext context)
        {
            using (result)
            {
                var headersTask = result.ResponseHeadersAsync;
                if (headersTask != null)
                {
                    var headers = await headersTask.ConfigureAwait(false);
                    if (headers != null) await context.WriteResponseHeadersAsync(headers).ConfigureAwait(false);
                }
                var value = await result.ConfigureAwait(false);
                var trailers = result.GetTrailers();
                if (trailers != null)
                {
                    foreach (var entry in trailers)
                        context.ResponseTrailers.Add(entry);
                }
                return value;
            }
        }

        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static async Task WriteTo<T>(AsyncServerStreamingCall<T> result, IServerStreamWriter<T> writer, ServerCallContext context)
        {
            using (result)
            {
                var headersTask = result.ResponseHeadersAsync;
                if (headersTask != null)
                {
                    var headers = await headersTask.ConfigureAwait(false);
                    if (headers != null) await context.WriteResponseHeadersAsync(headers).ConfigureAwait(false);
                }
                var reader = result.ResponseStream;
                if (reader != null)
                {
                    using (reader)
                    {
                        while (await reader.MoveNext(context.CancellationToken).ConfigureAwait(false))
                        {
                            await writer.WriteAsync(reader.Current).ConfigureAwait(false);
                        }
                    }
                }
                var trailers = result.GetTrailers();
                if (trailers != null)
                {
                    foreach (var entry in trailers)
                        context.ResponseTrailers.Add(entry);
                }
            }
        }
        */

        /// <summary>
        /// Interprets a stream-reader as an asynchronous enumerable sequence
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(this IAsyncStreamReader<T> reader, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (await reader.MoveNext(cancellationToken).ConfigureAwait(false))
            {
                yield return reader.Current;
            }
        }

        /// <summary>
        /// Interprets a stream-reader as an observable sequence
        /// </summary>
        public static IObservable<T> AsObservable<T>(this IAsyncStreamReader<T> reader)
            => new ReaderObservable<T>(reader);

        private sealed class ReaderObservable<T> : IObservable<T>, IDisposable
        {
            private readonly IAsyncStreamReader<T> _reader;
            private IObserver<T>? _observer;

            public ReaderObservable(IAsyncStreamReader<T> reader)
            {
                _reader = reader;
            }

            IDisposable IObservable<T>.Subscribe(IObserver<T> observer)
            {

                if (observer is null) ThrowNull();
                if (Interlocked.CompareExchange(ref _observer, observer, null) is not null) ThrowObserved();
                Task.Run(PushToObserver);
                return this;

                static void ThrowNull() => throw new ArgumentNullException(nameof(observer));
                static void ThrowObserved() => throw new InvalidOperationException("The sequence is already being observed");
            }
            public void Dispose() => Volatile.Write(ref _observer, null);
            private async Task PushToObserver()
            {
                // we don't *expect* eager dispose, and using a custom CT *on top of* the gRPC CT
                // makes for perf complications; instead, we'll optimize for "consume everything",
                // and in the rare occasion when the consumer stops early: we'll just handle it
                try
                {
                    while (await _reader.MoveNext(CancellationToken.None).ConfigureAwait(false))
                    {
                        Volatile.Read(ref _observer)?.OnNext(_reader.Current);
                    }
                    Volatile.Read(ref _observer)?.OnCompleted();
                }
                catch (Exception ex)
                {
                    Volatile.Read(ref _observer)?.OnError(ex);
                }
                finally
                {
                    Dispose();
                }
            }
        }

        /// <summary>
        /// Consumes an observable sequence and writes it to a server stream-writer
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static async Task WriteTo<T>(this IAsyncEnumerable<T> reader, IServerStreamWriter<T> writer, CancellationToken cancellationToken)
        {
            await foreach (var value in reader.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await writer.WriteAsync(value).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Consumes an asynchronous enumerable sequence and writes it to a server stream-writer
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static Task WriteTo<T>(this IObservable<T> reader, IAsyncStreamWriter<T> writer)
            => new WriterObserver<T>().Subscribe(reader, writer);

        private sealed class WriterObserver<T> : IObserver<T>, IValueTaskSource<bool>
#if NETCOREAPP3_1_OR_GREATER
            , IThreadPoolWorkItem
#endif
        {
#if NETCOREAPP3_1_OR_GREATER
            void IThreadPoolWorkItem.Execute() => Activate();
#else
            private static readonly WaitCallback s_Activate = static state => Unsafe.As<WriterObserver<T>>(state)!.Activate();
#endif

            private readonly Queue<T> _backlog = new Queue<T>();
            private ManualResetValueTaskSourceCore<bool> _pendingWork;
            [Flags]
            private enum StateFlags
            {
                None = 0,
                IsCompleted = 1 << 0,
                NeedsActivation = 1 << 1,
            }
            private StateFlags _flags;

            public async Task Subscribe(IObservable<T> reader, IAsyncStreamWriter<T> writer)
            {
                await Task.Yield();
                var sub = reader.Subscribe(this);
                try
                {
                    while (await WaitForWorkAsync().ConfigureAwait(false))
                    {
                        // try to read synchronously as much as possible
                        while (true)
                        {
                            T next;

                            lock (_backlog)
                            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
                                if (!_backlog.TryDequeue(out next!)) break;
#else
                                if (_backlog.Count == 0) break;
                                next = _backlog.Dequeue();
#endif
                            }
                            await writer.WriteAsync(next).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    lock (_backlog)
                    {   // we won't be writing any more; formalize that
                        _backlog.Clear();
                        _flags |= StateFlags.IsCompleted;
                    }
                    try
                    {
                        sub?.Dispose();
                    }
                    catch { }
                }
            }
            private ValueTask<bool> WaitForWorkAsync()
            {
                lock (_backlog)
                {
                    if (_backlog.Count != 0) return new ValueTask<bool>(true);
                    if ((_flags & StateFlags.IsCompleted) != 0) return new ValueTask<bool>(false);
                    _flags |= StateFlags.NeedsActivation;
                    return new ValueTask<bool>(this, _pendingWork.Version);
                }
            }

            private void Activate() => _pendingWork.SetResult(true); // note this value is a dummy; the real value comes from GetResult
            private void ActivateIfNeededLocked()
            {
                if ((_flags & StateFlags.NeedsActivation) != 0)
                {
                    _flags &= ~StateFlags.NeedsActivation;
#if NETCOREAPP3_1_OR_GREATER
                    ThreadPool.UnsafeQueueUserWorkItem(this, false);
#else
                    ThreadPool.UnsafeQueueUserWorkItem(s_Activate, this);
#endif
                }
            }
            void IObserver<T>.OnCompleted()
            {
                lock (_backlog)
                {
                    _flags |= StateFlags.IsCompleted;
                    ActivateIfNeededLocked();
                }
            }

            void IObserver<T>.OnError(Exception error)
            {
                lock (_backlog)
                {
                    _backlog.Clear(); // something bad happened; throw away the outstanding work
                    _flags |= StateFlags.IsCompleted;
                    ActivateIfNeededLocked();
                }
            }

            void IObserver<T>.OnNext(T value)
            {
                lock (_backlog)
                {
                    if ((_flags & StateFlags.IsCompleted) == 0)
                    {
                        _backlog.Enqueue(value);
                        ActivateIfNeededLocked();
                    }
                }
            }

            ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token)
                => _pendingWork.GetStatus(token);

            void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
                => _pendingWork.OnCompleted(continuation, state, token, flags);

            bool IValueTaskSource<bool>.GetResult(short token)
            {
                lock (_backlog)
                {
                    _pendingWork.GetResult(token); // discard the dummy value
                    _pendingWork.Reset();
                    return _backlog.Count != 0;
                }
            }
        }

        /// <summary>
        /// Consumes the provided task raising exceptions as <see cref="RpcException"/>
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static Task WithSimpleExceptionHandling(Task task)
        {
            return task.RanToCompletion() ? Task.CompletedTask : Awaited(task);

            static async Task Awaited(Task task)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (Exception ex) when (SimpleRpcExceptionsInterceptor.ShouldWrap(ex, out var status))
                {
                    throw new RpcException(status, ex.Message);
                }
            }
        }



        /// <summary>
        /// Consumes the provided task raising exceptions as <see cref="RpcException"/>
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static Task<T> WithSimpleExceptionHandling<T>(Task<T> task)
        {
            return task.RanToCompletion() ? task : Awaited(task);

            static async Task<T> Awaited(Task<T> task)
            {
                try
                {
                    return await task.ConfigureAwait(false);
                }
                catch (Exception ex) when (SimpleRpcExceptionsInterceptor.ShouldWrap(ex, out var status))
                {
                    throw new RpcException(status, ex.Message);
                }
            }
        }

        /// <summary>
        /// Performs a gRPC blocking unary call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResponse UnarySync<TRequest, TResponse>(
            this in CallContext context,
            CallInvoker invoker, Method<TRequest, TResponse> method, TRequest request, string? host = null)
            where TRequest : class
            where TResponse : class
        {
            var metadata = context.Prepare();
            try
            {
                return invoker.BlockingUnaryCall(method, host, context.CallOptions, request);
            }
            catch (RpcException fault)
            {
                metadata?.SetTrailers(fault);
                throw;
            }
        }

        /// <summary>
        /// Performs a gRPC blocking unary call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnarySyncVoid<TRequest, TResponse>(
            this in CallContext context,
            CallInvoker invoker, Method<TRequest, TResponse> method, TRequest request, string? host = null)
            where TRequest : class
            where TResponse : class
        {
            var metadata = context.Prepare();
            try
            {
                invoker.BlockingUnaryCall(method, host, context.CallOptions, request);
            }
            catch (RpcException fault)
            {
                metadata?.SetTrailers(fault);
                throw;
            }
        }

        /// <summary>
        /// Performs a gRPC asynchronous unary call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<TResponse> UnaryTaskAsync<TRequest, TResponse>(
            this in CallContext context,
            CallInvoker invoker, Method<TRequest, TResponse> method, TRequest request, string? host = null)
            where TRequest : class
            where TResponse : class
            => UnaryTaskAsyncImpl<TRequest, TResponse>(invoker.AsyncUnaryCall<TRequest, TResponse>(method, host, context.CallOptions, request),
                context.Prepare(), context.CancellationToken);

        /// <summary>
        /// Performs a gRPC asynchronous unary call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<TResponse> UnaryValueTaskAsync<TRequest, TResponse>(
            this in CallContext context, CallInvoker invoker,
            Method<TRequest, TResponse> method, TRequest request, string? host = null)
            where TRequest : class
            where TResponse : class
            => new ValueTask<TResponse>(UnaryTaskAsyncImpl<TRequest, TResponse>(
                invoker.AsyncUnaryCall<TRequest, TResponse>(method, host, context.CallOptions, request),
                context.Prepare(), context.CancellationToken));

        /// <summary>
        /// Performs a gRPC asynchronous unary call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask UnaryValueTaskAsyncVoid<TRequest, TResponse>(
            this in CallContext context, CallInvoker invoker,
            Method<TRequest, TResponse> method, TRequest request, string? host = null)
            where TRequest : class
            where TResponse : class
            => new ValueTask(UnaryTaskAsyncImpl<TRequest, TResponse>(invoker.AsyncUnaryCall<TRequest, TResponse>(method, host, context.CallOptions, request), context.Prepare(), context.CancellationToken));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task<TResponse> UnaryTaskAsyncImpl<TRequest, TResponse>(
            AsyncUnaryCall<TResponse> call, MetadataContext? metadata, CancellationToken cancellationToken)
        {
            using (call)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (metadata != null) await metadata.SetHeadersAsync(call.ResponseHeadersAsync).ConfigureAwait(false);

                TResponse value;
                try
                {
                    value = await call.ResponseAsync.ConfigureAwait(false);
                }
                catch (RpcException fault)
                {
                    metadata?.SetTrailers(fault);
                    throw;
                }
                metadata?.SetTrailers(call, c => c.GetStatus(), c => c.GetTrailers());

                return value;
            }
        }

        /// <summary>
        /// Performs a gRPC server-streaming call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IAsyncEnumerable<TResponse> ServerStreamingAsync<TRequest, TResponse>(
            this in CallContext context,
            CallInvoker invoker, Method<TRequest, TResponse> method, TRequest request, string? host = null)
            where TRequest : class
            where TResponse : class
            => ServerStreamingAsyncImpl<TRequest, TResponse>(invoker.AsyncServerStreamingCall<TRequest, TResponse>(method, host, context.CallOptions, request), context.Prepare(), context.CancellationToken);

        private static async IAsyncEnumerable<TResponse> ServerStreamingAsyncImpl<TRequest, TResponse>(
            AsyncServerStreamingCall<TResponse> call, MetadataContext? metadata,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // note that the compiler/runtime will combine the context+consumer cancellation tokens as required;
            // since we don't need to trigger our own cancellation, this is sufficient
            using (call)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (metadata != null) await metadata.SetHeadersAsync(call.ResponseHeadersAsync).ConfigureAwait(false);

                var seq = call.ResponseStream;

                bool haveMore;
                do
                {
                    try // this is a little awkward because we can't yield inside a try/catch,
                    {   // but we want to catch the RpcException to capture the outbound headers
                        haveMore = await seq.MoveNext(cancellationToken).ConfigureAwait(false);
                    }
                    catch (RpcException fault)
                    {   // note: the RpcException doesn't seem to carry trailers in the managed
                        // client; see https://github.com/grpc/grpc-dotnet/issues/915
                        metadata?.SetTrailers(fault);
                        throw;
                    }

                    if (haveMore)
                    {
                        yield return seq.Current;
                    }
                } while (haveMore);

                metadata?.SetTrailers(call, c => c.GetStatus(), c => c.GetTrailers());
            }
        }

        /// <summary>
        /// Performs a gRPC client-streaming call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<TResponse> ClientStreamingTaskAsync<TRequest, TResponse>(
            this in CallContext options,
            CallInvoker invoker, Method<TRequest, TResponse> method, IAsyncEnumerable<TRequest> request, string? host = null)
            where TRequest : class
            where TResponse : class
            => ClientStreamingTaskAsyncImpl(invoker.AsyncClientStreamingCall<TRequest, TResponse>(method, host, options.CallOptions), options.Prepare(), options.IgnoreStreamTermination, request, options.CancellationToken);

        /// <summary>
        /// Performs a gRPC client-streaming call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<TResponse> ClientStreamingValueTaskAsync<TRequest, TResponse>(
            this in CallContext options,
            CallInvoker invoker, Method<TRequest, TResponse> method, IAsyncEnumerable<TRequest> request, string? host = null)
            where TRequest : class
            where TResponse : class
            => new ValueTask<TResponse>(ClientStreamingTaskAsyncImpl(invoker.AsyncClientStreamingCall<TRequest, TResponse>(method, host, options.CallOptions), options.Prepare(), options.IgnoreStreamTermination, request, options.CancellationToken));

        /// <summary>
        /// Performs a gRPC client-streaming call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask ClientStreamingValueTaskAsyncVoid<TRequest, TResponse>(
            this in CallContext options,
            CallInvoker invoker, Method<TRequest, TResponse> method, IAsyncEnumerable<TRequest> request, string? host = null)
            where TRequest : class
            where TResponse : class
            => new ValueTask(ClientStreamingTaskAsyncImpl(invoker.AsyncClientStreamingCall<TRequest, TResponse>(method, host, options.CallOptions), options.Prepare(), options.IgnoreStreamTermination, request, options.CancellationToken));

        private static async Task<TResponse> ClientStreamingTaskAsyncImpl<TRequest, TResponse>(
            AsyncClientStreamingCall<TRequest, TResponse> call, MetadataContext? metadata,
            bool ignoreStreamTermination, IAsyncEnumerable<TRequest> request, CancellationToken cancellationToken)
        {
            using (call)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var allDone = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, default);

                if (metadata != null) await metadata.SetHeadersAsync(call.ResponseHeadersAsync).ConfigureAwait(false);

                // send all the data *before* we check for a reply
                try
                {
                    await SendAll(call.RequestStream, request, allDone, ignoreStreamTermination).ConfigureAwait(false);

                    allDone.Token.ThrowIfCancellationRequested();
                    var result = await call.ResponseAsync.ConfigureAwait(false);

                    metadata?.SetTrailers(call, c => c.GetStatus(), c => c.GetTrailers());
                    return result;
                }
                catch (RpcException fault)
                {
                    metadata?.SetTrailers(fault);
                    throw;
                }
            }
        }

        /// <summary>
        /// Performs a gRPC duplex call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IAsyncEnumerable<TResponse> DuplexAsync<TRequest, TResponse>(
            this in CallContext options,
            CallInvoker invoker, Method<TRequest, TResponse> method, IAsyncEnumerable<TRequest> request, string? host = null)
            where TRequest : class
            where TResponse : class
            => DuplexAsyncImpl<TRequest, TResponse>(invoker.AsyncDuplexStreamingCall<TRequest, TResponse>(method, host, options.CallOptions), options.Prepare(), options.IgnoreStreamTermination, request, options.CancellationToken);

        private static async IAsyncEnumerable<TResponse> DuplexAsyncImpl<TRequest, TResponse>(
            AsyncDuplexStreamingCall<TRequest, TResponse> call, MetadataContext? metadata,
            bool ignoreStreamTermination, IAsyncEnumerable<TRequest> request, CancellationToken contextCancel, [EnumeratorCancellation] CancellationToken consumerCancel = default)
        {
            using (call)
            {
                contextCancel.ThrowIfCancellationRequested();
                consumerCancel.ThrowIfCancellationRequested();

                // create a linked CTS that can trigger cancellation when any of:
                // - the context is cancelled
                // - the consumer specified cancellation
                // - the server indicates an end of the bidi stream
                Task sendAll;
                using var allDone = CancellationTokenSource.CreateLinkedTokenSource(contextCancel, consumerCancel);
                try
                {
                    // we'll run the "send" as a concurrent operation
                    sendAll = Task.Run(() => SendAll(call.RequestStream, request, allDone, ignoreStreamTermination), allDone.Token);

                    if (metadata != null) await metadata.SetHeadersAsync(call.ResponseHeadersAsync).ConfigureAwait(false);

                    var seq = call.ResponseStream;

                    bool haveMore;
                    do
                    {
                        try // this is a little awkward because we can't yield inside a try/catch,
                        {   // but we want to catch the RpcException to capture the outbound headers
                            haveMore = await seq.MoveNext(allDone.Token).ConfigureAwait(false);
                        }
                        catch (RpcException fault)
                        {
                            metadata?.SetTrailers(fault);
                            throw;
                        }

                        if (haveMore)
                        {
                            yield return seq.Current;
                        }
                    } while (haveMore);
                }
                finally
                {   // want to cancel the producer *however* we exit
                    allDone.Cancel();
                }

                try
                {
                    await sendAll.ConfigureAwait(false); // observe any problems from sending
                }
                catch (OperationCanceledException) { }
                catch (RpcException fault)
                {
                    metadata?.SetTrailers(fault);
                    throw;
                }
                metadata?.SetTrailers(call, c => c.GetStatus(), c => c.GetTrailers());
            }
        }

        static async Task SendAll<T>(IClientStreamWriter<T> output, IAsyncEnumerable<T> request, CancellationTokenSource allDone, bool ignoreStreamTermination)
        {
            if (allDone.IsCancellationRequested) allDone.Token.ThrowIfCancellationRequested();
            try
            {
                await foreach (var value in request.WithCancellation(allDone.Token).ConfigureAwait(false))
                {
                    try
                    {
                        if (ignoreStreamTermination && allDone.IsCancellationRequested) break;
                        await output.WriteAsync(value).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        if (ignoreStreamTermination) break;
                        throw new IncompleteSendRpcException(ex);
                    }

                    // happy to bomb out, as long as we weren't holding a value at the time
                    if (allDone.IsCancellationRequested) allDone.Token.ThrowIfCancellationRequested();
                }
                await output.CompleteAsync().ConfigureAwait(false);
            }
            catch
            {
                if (!allDone.IsCancellationRequested)
                {
                    try
                    {
                        allDone.Cancel();
                    }
                    catch { } // calls to "Cancel" can race, ignore the exception if we lose the race
                }
                throw;
            }
        }
    }
    internal sealed class IncompleteSendRpcException : Exception
    {
        public IncompleteSendRpcException(Exception fault) : base(
            $"A message could not be sent because the server had already terminated the connection; this exception can be suppressed by specifying the {nameof(CallContextFlags.IgnoreStreamTermination)} flag when creating the {nameof(CallContext)}", fault)
        { }
    }
}
