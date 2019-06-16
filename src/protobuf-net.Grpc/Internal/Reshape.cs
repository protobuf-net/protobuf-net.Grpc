using Grpc.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
namespace ProtoBuf.Grpc.Internal
{
    [Obsolete(WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public static class Reshape
    {
        internal const string WarningMessage = "This API is intended for use by runtime-generated code; all methods can be changed without notice - it is only guaranteed to work with the internally generated code";

        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(this IAsyncStreamReader<T> reader, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using (reader)
            {
                while (await reader.MoveNext(cancellationToken))
                {
                    yield return reader.Current;
                }
            }
        }

        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static async Task WriteTo<T>(this IAsyncEnumerable<T> reader, IServerStreamWriter<T> writer, CancellationToken cancellationToken)
        {
            await using (var iter = reader.GetAsyncEnumerator(cancellationToken))
            {
                while (await iter.MoveNextAsync())
                {
                    await writer.WriteAsync(iter.Current);
                }
            }
        }

        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static TResponse UnarySync<TRequest, TResponse>(
            this in CallContext context,
            CallInvoker invoker, Method<TRequest, TResponse> method, TRequest request, string? host = null)
            where TRequest : class
            where TResponse : class
        {
            context.Prepare();
            return invoker.BlockingUnaryCall(method, host, context.Client, request);
        }

        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static Task<TResponse> UnaryTaskAsync<TRequest, TResponse>(
            this in CallContext context,
            CallInvoker invoker, Method<TRequest, TResponse> method, TRequest request, string? host = null)
            where TRequest : class
            where TResponse : class
            => UnaryTaskAsyncImpl<TRequest, TResponse>(invoker.AsyncUnaryCall<TRequest, TResponse>(method, host, context.Client, request), context.Prepare());

        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static ValueTask<TResponse> UnaryValueTaskAsync<TRequest, TResponse>(
            this in CallContext context, CallInvoker invoker,
            Method<TRequest, TResponse> method, TRequest request, string? host = null)
            where TRequest : class
            where TResponse : class
            => new ValueTask<TResponse>(UnaryTaskAsyncImpl<TRequest, TResponse>(invoker.AsyncUnaryCall<TRequest, TResponse>(method, host, context.Client, request), context.Prepare()));

        private static async Task<TResponse> UnaryTaskAsyncImpl<TRequest, TResponse>(
            AsyncUnaryCall<TResponse> call, MetadataContext? metadata)
        {
            using (call)
            {
                if (metadata != null) metadata.Headers = await call.ResponseHeadersAsync;
                var value = await call;
                if (metadata != null)
                {
                    metadata.Trailers = call.GetTrailers();
                    metadata.Status = call.GetStatus();
                }
                return value;
            }
        }

        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static IAsyncEnumerable<TResponse> ServerStreamingAsync<TRequest, TResponse>(
            this in CallContext context,
            CallInvoker invoker, Method<TRequest, TResponse> method, TRequest request, string? host = null)
            where TRequest : class
            where TResponse : class
            => ServerStreamingAsyncImpl<TRequest, TResponse>(invoker.AsyncServerStreamingCall<TRequest, TResponse>(method, host, context.Client, request), context.Prepare(), context.CancellationToken);

        private static async IAsyncEnumerable<TResponse> ServerStreamingAsyncImpl<TRequest, TResponse>(
            AsyncServerStreamingCall<TResponse> call, MetadataContext? metadata,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using (call)
            {
                if (metadata != null) metadata.Headers = await call.ResponseHeadersAsync;

                using (var seq = call.ResponseStream)
                {
                    while (await seq.MoveNext(default))
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
        }

        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static Task<TResponse> ClientStreamingTaskAsync<TRequest, TResponse>(
            this in CallContext options,
            CallInvoker invoker, Method<TRequest, TResponse> method, IAsyncEnumerable<TRequest> request, string? host = null)
            where TRequest : class
            where TResponse : class
            => ClientStreamingTaskAsyncImpl(invoker.AsyncClientStreamingCall<TRequest, TResponse>(method, host, options.Client), options.Prepare(), options.CancellationToken, request);

        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static ValueTask<TResponse> ClientStreamingValueTaskAsync<TRequest, TResponse>(
            this in CallContext options,
            CallInvoker invoker, Method<TRequest, TResponse> method, IAsyncEnumerable<TRequest> request, string? host = null)
            where TRequest : class
            where TResponse : class
            => new ValueTask<TResponse>(ClientStreamingTaskAsyncImpl(invoker.AsyncClientStreamingCall<TRequest, TResponse>(method, host, options.Client), options.Prepare(), options.CancellationToken, request));

        private static async Task<TResponse> ClientStreamingTaskAsyncImpl<TRequest, TResponse>(
            AsyncClientStreamingCall<TRequest, TResponse> call, MetadataContext? metadata,
            CancellationToken cancellationToken, IAsyncEnumerable<TRequest> request)
        {
            using (call)
            {
                var output = call.RequestStream;
                await using (var iter = request.GetAsyncEnumerator(cancellationToken))
                {
                    while (await iter.MoveNextAsync())
                    {
                        await output.WriteAsync(iter.Current);
                    }
                }
                await output.CompleteAsync();

                if (metadata != null) metadata.Headers = await call.ResponseHeadersAsync;

                var result = await call.ResponseAsync;

                if (metadata != null)
                {
                    metadata.Trailers = call.GetTrailers();
                    metadata.Status = call.GetStatus();
                }
                return result;
            }
        }

        [Obsolete(WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static IAsyncEnumerable<TResponse> DuplexAsync<TRequest, TResponse>(
            this in CallContext options,
            CallInvoker invoker, Method<TRequest, TResponse> method, IAsyncEnumerable<TRequest> request, string? host = null)
            where TRequest : class
            where TResponse : class
            => DuplexAsyncImpl<TRequest, TResponse>(invoker.AsyncDuplexStreamingCall<TRequest, TResponse>(method, host, options.Client), options.Prepare(), options.CancellationToken, request);

        private static async IAsyncEnumerable<TResponse> DuplexAsyncImpl<TRequest, TResponse>(
            AsyncDuplexStreamingCall<TRequest, TResponse> call, MetadataContext? metadata,
            [EnumeratorCancellation] CancellationToken cancellationToken, IAsyncEnumerable<TRequest> request)
        {
            using (call)
            {
                // we'll run the "send" as a concurrent operation
                var sendAll = Task.Run(() => SendAll(call.RequestStream, request, cancellationToken));

                if (metadata != null) metadata.Headers = await call.ResponseHeadersAsync;

                using (var seq = call.ResponseStream)
                {
                    while (await seq.MoveNext(default))
                    {
                        yield return seq.Current;
                    }
                    await sendAll; // observe any problems from sending

                    if (metadata != null)
                    {
                        metadata.Trailers = call.GetTrailers();
                        metadata.Status = call.GetStatus();
                    }
                }
            }
        }

        private static async Task SendAll<T>(IClientStreamWriter<T> output, IAsyncEnumerable<T> request, CancellationToken cancellationToken)
        {
            try
            {
                await using (var iter = request.GetAsyncEnumerator(cancellationToken))
                {
                    while (await iter.MoveNextAsync())
                    {
                        var item = iter.Current;
                        await output.WriteAsync(item);
                    }
                }
                await output.CompleteAsync();
            }
            catch (TaskCanceledException) { }
        }
    }
}
