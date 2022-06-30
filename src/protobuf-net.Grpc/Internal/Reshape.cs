﻿using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
    public static partial class Reshape
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

        /// <summary>
        /// Interprets a stream-reader as an observable sequence
        /// </summary>
        public static Stream AsStream(this IAsyncStreamReader<StreamFragment> reader)
            => new GrpcFragmentStream(reader, null);

        private class ReaderObservable<T> : IObservable<T>, IDisposable
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
            public virtual void Dispose() => Volatile.Write(ref _observer, null);
            private async Task PushToObserver()
            {
                // we don't *expect* eager dispose, and using a custom CT *on top of* the gRPC CT
                // makes for perf complications; instead, we'll optimize for "consume everything",
                // and in the rare occasion when the consumer stops early: we'll just handle it
                try
                {
                    Debug.WriteLine("starting observer publish", ToString());
                    await OnBeforeAsync().ConfigureAwait(false);
                    while (await _reader.MoveNext(CancellationToken.None).ConfigureAwait(false))
                    {
                        Debug.WriteLine($"forwarding: {_reader.Current}", ToString());
                        Volatile.Read(ref _observer)?.OnNext(_reader.Current);
                    }
                    Debug.WriteLine($"completing observer publish", ToString());
                    await OnAfterAsync().ConfigureAwait(false);
                    Volatile.Read(ref _observer)?.OnCompleted();
                }
                catch (RpcException fault)
                {
                    Debug.WriteLine($"observer fault: {fault.Message}", ToString());
                    OnFault(fault);
                    Volatile.Read(ref _observer)?.OnError(fault);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"observer fault: {ex.Message}", ToString());
                    Volatile.Read(ref _observer)?.OnError(ex);
                }
                finally
                {
                    Dispose();
                }
            }
            protected virtual ValueTask OnBeforeAsync() => default;
            protected virtual void OnFault(RpcException fault) { }
            protected virtual ValueTask OnAfterAsync() => default;
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
        /// Consumes an observale sequence and writes it to a server stream-writer
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static Task WriteObservableTo<T>(this IObservable<T> reader, IAsyncStreamWriter<T> writer)
            => new WriterObserver<T>().Subscribe(reader, writer);

        /// <summary>
        /// Consumes a stream and writes it to a server stream-writer
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static async Task WriteStreamTo(this Stream source, IAsyncStreamWriter<StreamFragment> writer)
        {
            if (source is MemoryStream ms && ms.TryGetBuffer(out var segment))
            {
                if (segment.Count > 0)
                {
                    // TODO: avoid this extra rent/copy
                    byte[] leased = ArrayPool<byte>.Shared.Rent(segment.Count);
                    Buffer.BlockCopy(segment.Array!, segment.Offset, leased, 0, segment.Count);
                    var fragment = StreamFragment.Create(leased, segment.Count);
                    await writer.WriteAsync(fragment).ConfigureAwait(false);
                }
            }
            else
            {
                while (true)
                {
                    // TODO: overlapped reads and writes?
                    const int CHUNK_SIZE = 16 * 1024;
                    byte[] leased = ArrayPool<byte>.Shared.Rent(CHUNK_SIZE);
                    var length = await source.ReadAsync(leased, 0, leased.Length).ConfigureAwait(false);

                    var fragment = StreamFragment.Create(leased, length); // note: this also does a handy recycle for zero lengths
                    if (length <= 0) break;

                    await writer.WriteAsync(fragment).ConfigureAwait(false);
                    // fragment.Dispose();
                }
            }
            if (writer is IClientStreamWriter<StreamFragment> client)
            {
                await client.CompleteAsync().ConfigureAwait(false);
            }
        }

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
            public WriterObserver()
            {
                Debug.WriteLine("Creating...", ToString());
            }
            private readonly Queue<T> _backlog = new Queue<T>();
            private volatile Exception? _fault;
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
                Debug.WriteLine($"Subscribing...", ToString());
                await Task.Yield();
                var sub = reader?.Subscribe(this);
                string debugStep = nameof(Subscribe);
                try
                {
                    if (reader is null) return; // nothing to write
                    debugStep = nameof(WaitForWorkAsync);
                    while (await WaitForWorkAsync().ConfigureAwait(false))
                    {
                        // try to read synchronously as much as possible
                        while (true)
                        {
                            T next;

                            debugStep = nameof(_backlog.Dequeue);
                            lock (_backlog)
                            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
                                if (!_backlog.TryDequeue(out next!)) break;
#else
                                if (_backlog.Count == 0) break;
                                next = _backlog.Dequeue();
#endif
                            }
                            Debug.WriteLine($"Writing: {next}", ToString());
                            debugStep = nameof(writer.WriteAsync);
                            await writer.WriteAsync(next).ConfigureAwait(false);
                        }
                    }
                    Debug.WriteLine($"Subscribe exiting with success", ToString());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Subscribe exiting with failure during {debugStep}: {ex.Message}", ToString());
                    throw;
                }
                finally
                {
                    lock (_backlog)
                    {   // we won't be writing any more; formalize that
                        _backlog.Clear();
                        _flags |= StateFlags.IsCompleted;
                    }

                    if (writer is IClientStreamWriter<T> client)
                    {
                        try // tell the writer that we're done, if possible
                        {
                            await client.CompleteAsync().ConfigureAwait(false);
                        }
                        catch { }
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
                    Debug.WriteLine("Subscribe awaiting reactivation", ToString());
                    return new ValueTask<bool>(this, _pendingWork.Version);
                }
            }

            private void Activate()
            {
                var fault = _fault;
                if (fault is null)
                {
                    _pendingWork.SetResult(true); // note this value is a dummy; the real value comes from GetResult
                }
                else
                {
                    _pendingWork.SetException(fault);
                }
            }
            private void ActivateIfNeededLocked()
            {
                if ((_flags & StateFlags.NeedsActivation) != 0)
                {
                    _flags &= ~StateFlags.NeedsActivation;
                    Debug.WriteLine("Activating", ToString());
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
                    Debug.WriteLine("Completing", ToString());
                    _flags |= StateFlags.IsCompleted;
                    ActivateIfNeededLocked();
                }
            }

            void IObserver<T>.OnError(Exception error)
            {
                lock (_backlog)
                {
                    Debug.WriteLine("Faulting", ToString());
                    _fault = error;
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
                        Debug.WriteLine($"Adding work: {value}", ToString());
                        _backlog.Enqueue(value);
                        ActivateIfNeededLocked();
                    }
                    else
                    {
                        Debug.WriteLine("Ignoring work: already completed", ToString());
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
            => ServerStreamingAsyncImpl<TResponse>(invoker.AsyncServerStreamingCall<TRequest, TResponse>(method, host, context.CallOptions, request), context.Prepare(), context.CancellationToken);

        /// <summary>
        /// Performs a gRPC server-streaming call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IObservable<TResponse> ServerStreamingObservable<TRequest, TResponse>(
            this in CallContext context,
            CallInvoker invoker, Method<TRequest, TResponse> method, TRequest request, string? host = null)
            where TRequest : class
            where TResponse : class
            => new ServerStreamingObservableImpl<TResponse>(invoker.AsyncServerStreamingCall<TRequest, TResponse>(method, host, context.CallOptions, request), context.Prepare());

        private static async IAsyncEnumerable<TResponse> ServerStreamingAsyncImpl<TResponse>(
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

        private sealed class ServerStreamingObservableImpl<TResponse> : ReaderObservable<TResponse>
        {
            private AsyncServerStreamingCall<TResponse>? _call;
            private MetadataContext? _metadata;
            public ServerStreamingObservableImpl(AsyncServerStreamingCall<TResponse> call, MetadataContext? metadata) : base(call.ResponseStream)
            {
                _call = call;
                _metadata = metadata;
            }

            public override void Dispose()
            {
                base.Dispose();
                var call = _call;
                _call = null;
                call?.Dispose();
            }

            protected override ValueTask OnBeforeAsync()
            {
                var metadata = _metadata;
                return metadata is null ? default : metadata.SetHeadersAsync(_call?.ResponseHeadersAsync);
            }

            protected override ValueTask OnAfterAsync()
            {
                _metadata?.SetTrailers(_call, c => c.GetStatus(), c => c.GetTrailers());
                return default;
            }

            protected override void OnFault(RpcException fault)
            {
                _metadata?.SetTrailers(fault);
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
        /// Performs a gRPC client-streaming call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<TResponse> ClientStreamingObservableTaskAsync<TRequest, TResponse>(
            this in CallContext options,
            CallInvoker invoker, Method<TRequest, TResponse> method, IObservable<TRequest> request, string? host = null)
            where TRequest : class
            where TResponse : class
            => ClientStreamingObservableTaskAsyncImpl(invoker.AsyncClientStreamingCall<TRequest, TResponse>(method, host, options.CallOptions), options.Prepare(), request);

        /// <summary>
        /// Performs a gRPC client-streaming call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<TResponse> ClientStreamingObservableValueTaskAsync<TRequest, TResponse>(
            this in CallContext options,
            CallInvoker invoker, Method<TRequest, TResponse> method, IObservable<TRequest> request, string? host = null)
            where TRequest : class
            where TResponse : class
            => new ValueTask<TResponse>(ClientStreamingObservableTaskAsyncImpl(invoker.AsyncClientStreamingCall<TRequest, TResponse>(method, host, options.CallOptions), options.Prepare(), request));

        /// <summary>
        /// Performs a gRPC client-streaming call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask ClientStreamingObservableValueTaskAsyncVoid<TRequest, TResponse>(
            this in CallContext options,
            CallInvoker invoker, Method<TRequest, TResponse> method, IObservable<TRequest> request, string? host = null)
            where TRequest : class
            where TResponse : class
            => new ValueTask(ClientStreamingObservableTaskAsyncImpl(invoker.AsyncClientStreamingCall<TRequest, TResponse>(method, host, options.CallOptions), options.Prepare(), request));

        private static async Task<TResponse> ClientStreamingObservableTaskAsyncImpl<TRequest, TResponse>(
            AsyncClientStreamingCall<TRequest, TResponse> call, MetadataContext? metadata,
            IObservable<TRequest> request)
        {
            using (call)
            {
                if (metadata != null) await metadata.SetHeadersAsync(call.ResponseHeadersAsync).ConfigureAwait(false);

                // send all the data *before* we check for a reply
                try
                {
                    await request.WriteObservableTo(call.RequestStream).ConfigureAwait(false);

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

        /// <summary>
        /// Performs a gRPC duplex call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Stream DuplexStreamStreamAsync<TRequest, TResponse>(
            this in CallContext options,
            CallInvoker invoker, Method<TRequest, TResponse> method, Stream request, string? host = null)
            where TRequest : class
            where TResponse : class
        {
            var call = (AsyncDuplexStreamingCall<StreamFragment, StreamFragment>)(object)invoker.AsyncDuplexStreamingCall<TRequest, TResponse>(method, host, options.CallOptions);

            Task.Run(() => WriteStreamTo(request, call.RequestStream));
            return new GrpcFragmentStream(call.ResponseStream, call);
        }
        //    => DuplexStreamStreamImpl(call, options.Prepare(), options.IgnoreStreamTermination, request, options.CancellationToken);

        private sealed class GrpcFragmentStream : Stream
        {
            private readonly object? _parent;
            private readonly IAsyncStreamReader<StreamFragment> _reader;
            private Task<bool>? _pendingMoveNext;

            private int _offset;
            private StreamFragment _current = StreamFragment.Empty;

            public GrpcFragmentStream(IAsyncStreamReader<StreamFragment> reader, object? parent)
            {
                _reader = reader;
                _parent = parent;
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    ReleaseCurrent();
                    if (_parent is IDisposable d) d.Dispose();
                }
            }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
            public override ValueTask DisposeAsync()
            {
                ReleaseCurrent();
                switch (_parent)
                {
                    case IAsyncDisposable ad: return ad.DisposeAsync();
                    case IDisposable d: d.Dispose(); break;
                }
                return default;
            }

            public override void CopyTo(Stream destination, int bufferSize)
            {
                do
                {
                    var chunk = _current.TakeSegment(ref _offset, int.MaxValue);
                    if (chunk.Count != 0) destination.Write(chunk.Array!, chunk.Offset, chunk.Count);
                }
                while (ReadNextSync());
            }
#endif

            public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                do
                {
                    var chunk = _current.TakeSegment(ref _offset, int.MaxValue);
                    if (chunk.Count != 0) destination.Write(chunk.Array!, chunk.Offset, chunk.Count);
                }
                while (await ReadNextAsync(cancellationToken).ConfigureAwait(false));
            }

            public override bool CanWrite => false;
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanTimeout => true;
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();
            public override void WriteByte(byte value) => throw new NotSupportedException();
            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => throw new NotSupportedException();
            public override void EndWrite(IAsyncResult asyncResult) => throw new NotSupportedException();
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
            public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new NotSupportedException();
#endif
            public override long Length => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Flush() => throw new NotSupportedException();
            public override Task FlushAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }
            public override int WriteTimeout
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                var chunk = _current.TakeSegment(ref _offset, count);
                if (chunk.Count == 0 && !IsSyncCheck(count)) return ReadSlow(buffer, offset, count);
                Buffer.BlockCopy(chunk.Array!, chunk.Offset, buffer, offset, chunk.Count);
                return chunk.Count;
            }

            private bool IsSyncCheck(int count) => count == 0 && _current.Length >= _offset;

            [MethodImpl(MethodImplOptions.NoInlining)]
            private int ReadSlow(byte[] buffer, int offset, int count)
                => ReadNextSync() ? Read(buffer, offset, count) : 0;

            public override int ReadTimeout { get; set; }
            private bool ReadNextSync()
            {
                if (_pendingMoveNext is null) _pendingMoveNext = _reader.MoveNext();
                var timeout = ReadTimeout;
                var guid = Guid.NewGuid();
                while (true)
                {
                    ReleaseCurrent();
                    if (timeout <= 0 || timeout == int.MaxValue)
                    {
                        _pendingMoveNext.Wait();
                    }
                    else
                    {
                        if (!_pendingMoveNext.Wait(timeout)) throw new TimeoutException();
                    }
                    bool moveNext = _pendingMoveNext.Result;
                    _pendingMoveNext = null;
                    if (!moveNext) return false;
                    

                    _current = _reader.Current;
                    Debug.WriteLine($"Received (sync): {_current} from {_reader.GetType().Name}");
                    // only return non-empty fragments (we don't actually expect empty fragments, note!)
                    if (_current.Length > 0)
                    {
                        Debug.Assert(!_current.IsDisposed, $"received dead payload");
                        return true;
                    }

                    _pendingMoveNext = _reader.MoveNext();
                }
            }
            public override int ReadByte()
            {
                var chunk = _current.TakeSpan(ref _offset, 1);
                if (chunk.IsEmpty) return ReadByteSlow();
                return chunk[0];
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private int ReadByteSlow()
                => ReadNextSync() ? ReadByte() : -1;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
            public override int Read(Span<byte> buffer)
            {
                var chunk = _current.TakeSpan(ref _offset, buffer.Length);
                if (chunk.IsEmpty && !IsSyncCheck(buffer.Length)) return ReadSlow(buffer);
                chunk.CopyTo(buffer);
                return chunk.Length;
            }
            [MethodImpl(MethodImplOptions.NoInlining)]
            private int ReadSlow(Span<byte> buffer)
                => ReadNextSync() ? Read(buffer) : 0;

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var chunk = _current.TakeSegment(ref _offset, count);
                if (chunk.Count == 0 && !IsSyncCheck(count)) return ReadSlowAsync(buffer, offset, count, cancellationToken);
                Buffer.BlockCopy(chunk.Array!, chunk.Offset, buffer, offset, chunk.Count);
                return Task.FromResult<int>(chunk.Count);
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                var chunk = _current.TakeSpan(ref _offset, buffer.Length);
                if (chunk.IsEmpty && !IsSyncCheck(buffer.Length)) return ReadSlowAsync(buffer, cancellationToken);
                chunk.CopyTo(buffer.Span);
                return new(chunk.Length);
            }

            private ValueTask<int> ReadSlowAsync(Memory<byte> buffer, CancellationToken cancellationToken)
            {
                var pending = ReadNextAsync(cancellationToken);
                if (!pending.IsCompletedSuccessfully) return ReadSlowIncompleteAsync(pending, buffer, cancellationToken);
                return pending.Result ? new(Read(buffer.Span)) : new(0);
            }

            private async ValueTask<int> ReadSlowIncompleteAsync(ValueTask<bool> pending, Memory<byte> buffer, CancellationToken cancellationToken)
                => (await pending.ConfigureAwait(false)) ? Read(buffer.Span) : 0;
#endif
            private async Task<int> ReadSlowAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => await ReadNextAsync(cancellationToken).ConfigureAwait(false) ? Read(buffer, offset, count) : 0;

            private ValueTask<bool> ReadNextAsync(CancellationToken cancellationToken)
            {
                if (_pendingMoveNext is null) _pendingMoveNext = _reader.MoveNext();
                while (true)
                {
                    ReleaseCurrent();
                    if (!_pendingMoveNext.IsCompleted) return ReadNextIncompleteAsync(cancellationToken);

                    var moveNext = _pendingMoveNext.Result;
                    _pendingMoveNext = null;
                    if (!moveNext) return new(false);
                    

                    _current = _reader.Current;
                    Debug.WriteLine($"Received (async): {_current} from {_reader.GetType().Name}");
                    // only return non-empty fragments (we don't actually expect empty fragments, note!)
                    if (_current.Length > 0)
                    {
                        Debug.Assert(!_current.IsDisposed, $"received dead payload");
                        Debug.Assert(!_current.IsDisposed);
                        return new(true);
                    }

                    _pendingMoveNext = _reader.MoveNext(cancellationToken);
                }
            }

            private async ValueTask<bool> ReadNextIncompleteAsync(CancellationToken cancellationToken)
            {
                while (true)
                {
                    ReleaseCurrent();
                    var moveNext = await _pendingMoveNext!.ConfigureAwait(false);
                    _pendingMoveNext = null;
                    if (!moveNext) return false;

                    _current = _reader.Current;
                    // only return non-empty fragments (we don't actually expect empty fragments, note!)
                    if (_current.Length > 0) return true;

                    _pendingMoveNext = _reader.MoveNext(cancellationToken);
                }
            }

            private void ReleaseCurrent()
            {
                _current.Dispose();
                _current = StreamFragment.Empty;
                _offset = 0;
            }
        }


        /// <summary>
        /// Performs a gRPC duplex call
        /// </summary>
        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IObservable<TResponse> DuplexObservable<TRequest, TResponse>(
            this in CallContext options,
            CallInvoker invoker, Method<TRequest, TResponse> method, IObservable<TRequest> request, string? host = null)
            where TRequest : class
            where TResponse : class
            => new DuplexObservableImpl<TRequest, TResponse>(invoker.AsyncDuplexStreamingCall<TRequest, TResponse>(method, host, options.CallOptions), options.Prepare(), request);

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

        private sealed class DuplexObservableImpl<TRequest, TResponse> : ReaderObservable<TResponse>
        {
            private AsyncDuplexStreamingCall<TRequest, TResponse>? _call;
            private MetadataContext? _metadata;
            private readonly Task _sendAll;
            public DuplexObservableImpl(AsyncDuplexStreamingCall<TRequest, TResponse> call, MetadataContext? metadata, IObservable<TRequest> request) : base(call.ResponseStream)
            {
                _call = call;
                _metadata = metadata;
                _sendAll = request.WriteObservableTo(call.RequestStream);
            }

            public override void Dispose()
            {
                base.Dispose();
                var call = _call;
                _call = null;
                call?.Dispose();
            }

            protected override ValueTask OnBeforeAsync()
            {
                var metadata = _metadata;
                return metadata is null ? default : metadata.SetHeadersAsync(_call?.ResponseHeadersAsync);
            }

            protected override async ValueTask OnAfterAsync()
            {
                await _sendAll;
                _metadata?.SetTrailers(_call, c => c.GetStatus(), c => c.GetTrailers());
            }

            protected override void OnFault(RpcException fault)
                => _metadata?.SetTrailers(fault);
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
