using Grpc.Core;
using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Internal;

partial class Reshape
{
    /// <summary>
    /// Performs an operation that returns data from the server as a <see cref="Stream"/>.
    /// </summary>
    [Obsolete(WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Stream> ServerByteStreamingValueTaskAsync<TRequest, TResponse>(
        in CallContext context,
        CallInvoker invoker, Method<TRequest, TResponse> method, TRequest request, string? host = null)
        where TRequest : class
        => new(ServerByteStreamingTaskAsync(in context, invoker, method, request, host));

    /// <summary>
    /// Performs an operation that returns data from the server as a <see cref="Stream"/>.
    /// </summary>
    [Obsolete(WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<Stream> ServerByteStreamingTaskAsync<TRequest, TResponse>(
        in CallContext context,
        CallInvoker invoker, Method<TRequest, TResponse> method, TRequest request, string? host = null)
        where TRequest : class
    {

        context.CallOptions.CancellationToken.ThrowIfCancellationRequested();
        return Chunkify(invoker.AsyncServerStreamingCall(Assert<TRequest, BytesValue>(method), host, context.CallOptions, request), context.Prepare(), context.CancellationToken);

        async static Task<Stream> Chunkify(AsyncServerStreamingCall<BytesValue> call, MetadataContext? metadata, CancellationToken cancellationToken)
        {
            try
            {
                // wait for headers, even if not available; that means we're in a state to start spoofing the stream
                if (metadata is not null)
                {
                    await metadata.SetHeadersAsync(call.ResponseHeadersAsync);
                }
                else
                {
                    // even if we aren't capturing headers, we want to wait for them to be available,
                    //
                    await call.ResponseHeadersAsync.ConfigureAwait(false);
                }

                // so if we got this far, we think the server is happy - start spinning up infrastructure to be the stream
                Pipe pipe = new();
                _ = Task.Run(() => PushAsync(call, pipe.Writer, metadata, cancellationToken), CancellationToken.None);
                return pipe.Reader.AsStream(leaveOpen: false);
            }
            catch (RpcException fault)
            {
                metadata?.SetTrailers(fault);
                call.Dispose(); // note not using; only in case of fault!
                throw;
            }
        }

        async static Task PushAsync(AsyncServerStreamingCall<BytesValue> call, PipeWriter destination, MetadataContext? metadata, CancellationToken cancellationToken)
        {
            Exception? fault = null;
            try
            {
                var source = call.ResponseStream;
                while (await source.MoveNext(CancellationToken.None).ConfigureAwait(false)) // note that the context's cancellation is already baked in
                {
                    var chunk = source.Current;
                    var result = await destination.WriteAsync(chunk.Memory, cancellationToken).ConfigureAwait(false);
                    if (result.IsCanceled)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        FallbackThrowCanceled();
                    }
                    if (result.IsCompleted)
                    {
                        // reader has shut down; stop copying (we'll tell the server by disposing the call)
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                fault = ex;
                if (fault is RpcException rpcFault)
                {
                    metadata?.SetTrailers(rpcFault);
                }
                throw;
            }
            finally
            {
                try
                {
                    // signal that no more data will be written, or at least try!
                    await destination.CompleteAsync(fault).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
                try
                {
                    call.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
        }
    }

    /// <summary>
    /// Consumes an asynchronous enumerable sequence and writes it to a server stream-writer
    /// </summary>
    [Obsolete(WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public static async Task WriteStream(Task<Stream> source, IAsyncStreamWriter<BytesValue> writer, CancellationToken cancellationToken)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        await // IDisposable is up-level
#endif
        using var stream = await source;

        // read from the stream and write to writer
        int size = 512; // start modest and increase

        while (true)
        {
            byte[] leased = ArrayPool<byte>.Shared.Rent(size);

            var maxRead = Math.Min(leased.Length, BytesValue.MaxLength);
            var bytes = await stream.ReadAsync(leased, 0, maxRead, cancellationToken).ConfigureAwait(false);
            if (bytes <= 0) // EOF
            {
                ArrayPool<byte>.Shared.Return(leased);
                return;
            }
            if (bytes == maxRead)
            {
                // allow more next time
                size = Math.Min(size * 2, BytesValue.MaxLength);
            }
            else
            {
                // allow less next time, down to whatever we read
                size = Math.Max(maxRead, 128);
            }

            var chunk = new BytesValue(leased, bytes, pooled: true);
            await writer.WriteAsync(chunk).ConfigureAwait(false);
        }
    }

    static void FallbackThrowCanceled() => throw new OperationCanceledException();

    static Method<TRequest, TResponse> Assert<TRequest, TResponse>(IMethod method)
    {
        var typed = method as Method<TRequest, TResponse>;
        if (typed is null)
        {
            ThrowMethodFail(typeof(TRequest), typeof(TResponse));
        }
        return typed!;
    }

#pragma warning disable IDE0079 // (unnecessary suppression)
#pragma warning disable CA2208 // usage of literal "method"
    static void ThrowMethodFail(Type request, Type response) => throw new ArgumentException($"Method was expected to take '{request.Name}' and return '{response.Name}'", "method");
#pragma warning restore CA2208
#pragma warning restore IDE0079
}
