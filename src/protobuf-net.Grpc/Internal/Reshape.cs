using Grpc.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
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
#if TASK_COMPLETED
            if (task.IsCompletedSuccessfully) return Empty.InstanceTask;
#else
            if (task.Status == TaskStatus.RanToCompletion) return Empty.InstanceTask;
#endif

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
        /// Consumes an asynchronous enumerable sequence and writes it to a server stream-writer
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
            context.Prepare();
            return invoker.BlockingUnaryCall(method, host, context.CallOptions, request);
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
            context.Prepare();
            invoker.BlockingUnaryCall(method, host, context.CallOptions, request);
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

                if (metadata != null) metadata.Headers = await call.ResponseHeadersAsync.ConfigureAwait(false);
                var value = await call.ResponseAsync.ConfigureAwait(false);
                if (metadata != null)
                {
                    metadata.Trailers = call.GetTrailers();
                    metadata.Status = call.GetStatus();
                }
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

                if (metadata != null) metadata.Headers = await call.ResponseHeadersAsync.ConfigureAwait(false);

                var seq = call.ResponseStream;
                while (await seq.MoveNext(cancellationToken).ConfigureAwait(false))
                {
                    yield return seq.Current;
                }
                if (metadata != null)
                {
                    metadata.Trailers = call.GetTrailers();
                    metadata.Status = call.GetStatus();
                }
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
            => ClientStreamingTaskAsyncImpl(invoker.AsyncClientStreamingCall<TRequest, TResponse>(method, host, options.CallOptions), options.Prepare(), options.CancellationToken, request);

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
            => new ValueTask<TResponse>(ClientStreamingTaskAsyncImpl(invoker.AsyncClientStreamingCall<TRequest, TResponse>(method, host, options.CallOptions), options.Prepare(), options.CancellationToken, request));

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
            => new ValueTask(ClientStreamingTaskAsyncImpl(invoker.AsyncClientStreamingCall<TRequest, TResponse>(method, host, options.CallOptions), options.Prepare(), options.CancellationToken, request));

        private static async Task<TResponse> ClientStreamingTaskAsyncImpl<TRequest, TResponse>(
            AsyncClientStreamingCall<TRequest, TResponse> call, MetadataContext? metadata,
            CancellationToken cancellationToken, IAsyncEnumerable<TRequest> request)
        {
            using (call)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var output = call.RequestStream;
                await foreach (var value in request.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await output.WriteAsync(value).ConfigureAwait(false);
                    }
                    catch (RpcException rpc) when (rpc.StatusCode == StatusCode.OK)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        throw new IncompleteSendRpcException(rpc);
                    }
                }
                await output.CompleteAsync().ConfigureAwait(false);

                if (metadata != null) metadata.Headers = await call.ResponseHeadersAsync.ConfigureAwait(false);

                var result = await call.ResponseAsync.ConfigureAwait(false);

                if (metadata != null)
                {
                    metadata.Trailers = call.GetTrailers();
                    metadata.Status = call.GetStatus();
                }
                return result;
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
            => DuplexAsyncImpl<TRequest, TResponse>(invoker.AsyncDuplexStreamingCall<TRequest, TResponse>(method, host, options.CallOptions), options.Prepare(), options.CancellationToken, request);

        private static async IAsyncEnumerable<TResponse> DuplexAsyncImpl<TRequest, TResponse>(
            AsyncDuplexStreamingCall<TRequest, TResponse> call, MetadataContext? metadata,
            CancellationToken contextCancel, IAsyncEnumerable<TRequest> request, [EnumeratorCancellation] CancellationToken consumerCancel = default)
        {
            using (call)
            {
                contextCancel.ThrowIfCancellationRequested();
                consumerCancel.ThrowIfCancellationRequested();
                // create a linked CTS that can trigger cancellation when any of:
                // - the context is cancelled
                // - the consumer specified cancellation
                // - the server indicates an end of the bidi stream
                using var allDone = CancellationTokenSource.CreateLinkedTokenSource(contextCancel, consumerCancel);

                // we'll run the "send" as a concurrent operation
                var sendAll = Task.Run(() => SendAll(call.RequestStream, request, allDone.Token), allDone.Token);

                if (metadata != null) metadata.Headers = await call.ResponseHeadersAsync.ConfigureAwait(false);

                var seq = call.ResponseStream;
                while (await seq.MoveNext(allDone.Token).ConfigureAwait(false))
                {
                    yield return seq.Current;
                }

                allDone.Cancel();
                await sendAll.ConfigureAwait(false); // observe any problems from sending

                if (metadata != null)
                {
                    metadata.Trailers = call.GetTrailers();
                    metadata.Status = call.GetStatus();
                }
            }

            static async Task SendAll<T>(IClientStreamWriter<T> output, IAsyncEnumerable<T> request, CancellationToken cancellationToken)
            {
                try
                {
                    await foreach (var value in request.WithCancellation(cancellationToken).ConfigureAwait(false))
                    {
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await output.WriteAsync(value).ConfigureAwait(false);
                        }
                        catch (RpcException rpc) when (rpc.StatusCode == StatusCode.OK)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            throw new IncompleteSendRpcException(rpc);
                        }
                    }
                    await output.CompleteAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
            }
        }
    }
    internal sealed class IncompleteSendRpcException : Exception
    {
        public IncompleteSendRpcException(RpcException rpc) : base(
            "A message could not be sent because the server had already terminated the connection", rpc)
        { }
    }
}
