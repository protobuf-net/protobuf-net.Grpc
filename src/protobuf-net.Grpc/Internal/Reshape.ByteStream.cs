using Grpc.Core;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
        return ReadByteValueSequenceAsStream(invoker.AsyncServerStreamingCall(Assert<TRequest, BytesValue>(method), host, context.CallOptions, request), context.Prepare(), context.CancellationToken);

        async static Task<Stream> ReadByteValueSequenceAsStream(AsyncServerStreamingCall<BytesValue> call, MetadataContext? metadata, CancellationToken cancellationToken)
        {
            const bool DemandTrailer = false; // don't *demand* the trailer indicating total length, but enforce it if we find it (we always send it, currently)
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
                var firstRead = call.ResponseStream.MoveNext(CancellationToken.None);
                if (firstRead.IsCompleted)
                {
                    // probably an error in the call; fetch eagerly, so we can fail *before*
                    // providing a stream that needs reading to expose the fault; we'll
                    // touch the .Result, which is fine - we know it is completed, and this is
                    // Task, not ValueTask, so it is repeatable; if it throws: server fault;
                    // if it returns false, empty stream
                    if (!firstRead.GetAwaiter().GetResult())
                    {
                        // empty stream, which could be valid zero-length, or could be
                        // a server fault; we'll find out
                        metadata?.SetTrailers(call);
                        call.Dispose();
                        return Stream.Null;
                    }

                    // if we get here, the first read was success - that just means
                    // the server is fast; we'll just let the main path await the already-completed
                    // first read, like normal
                }

                // so if we got this far, we think the server is happy - start spinning up infrastructure to be the stream
                Pipe pipe = new();
                _ = Task.Run(() => ReadByteValueSequenceToPipeWriter(call, firstRead, pipe.Writer, metadata, DemandTrailer, cancellationToken), CancellationToken.None);
                return pipe.Reader.AsStream(leaveOpen: false);
            }
            catch (RpcException fault)
            {
                metadata?.SetTrailers(fault);
                call.Dispose(); // note not using; only in case of fault!
                throw;
            }
        }

        async static Task ReadByteValueSequenceToPipeWriter(AsyncServerStreamingCall<BytesValue> call, Task<bool> pendingRead, PipeWriter destination, MetadataContext? metadata, bool demandTrailer, CancellationToken cancellationToken)
        {
            Exception? fault = null;
            try
            {
                var source = call.ResponseStream;
                long actualLength = 0;
                bool clientTerminated = false;
                while (await pendingRead.ConfigureAwait(false)) // note that the context's cancellation is already baked in
                {
                    var chunk = source.Current;
                    var result = await destination.WriteAsync(chunk.Memory, cancellationToken).ConfigureAwait(false);
                    actualLength += chunk.Length;
                    chunk.Recycle();

                    if (result.IsCanceled)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        FallbackThrowCanceled();
                    }

                    if (result.IsCompleted)
                    {
                        // reader has shut down; stop copying (we'll tell the server by disposing the call)
                        clientTerminated = true;
                        demandTrailer = false;
                        break;
                    }

                    pendingRead = source.MoveNext(CancellationToken.None);
                }
                string? lenTrailer;
                try
                {
                    lenTrailer = call.GetTrailers().GetString(TrailerStreamLength);
                }
                catch (InvalidOperationException) when (clientTerminated)
                {
                    // we didn't let the stream get to the end; the trailers simply might not be there
                    lenTrailer = null;
                    metadata = null;
                }

                if (string.IsNullOrWhiteSpace(lenTrailer))
                {
                    if (demandTrailer) throw new InvalidOperationException($"Missing trailer: '{TrailerStreamLength}'");
                }
                else if (!long.TryParse(lenTrailer, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expectedLength)
                    || expectedLength != actualLength)
                {
                    throw new InvalidOperationException($"Invalid trailer or length mismatch: '{TrailerStreamLength}'");
                }
                metadata?.SetTrailers(call);
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
    /// Consumes an asynchronous enumerable sequence and exposes it as a byte-stream
    /// </summary>
    [Obsolete(WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public static Stream ReadStream(IAsyncStreamReader<BytesValue> writer, ServerCallContext context)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Performs a gRPC client-streaming call consuming a byte-stream
    /// </summary>
    [Obsolete(WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResponse> ClientByteStreamingTaskAsync<TRequest, TResponse>(
        this in CallContext options,
        CallInvoker invoker, Method<TRequest, TResponse> method, Stream request, string? host = null)
        where TRequest : class
        where TResponse : class
        => ClientByteStreamingImplAsync(invoker.AsyncClientStreamingCall(Assert<BytesValue, TResponse>(method), host, options.CallOptions), request, options.Prepare(), options.CancellationToken);

    /// <summary>
    /// Performs a gRPC client-streaming call consuming a byte-stream
    /// </summary>
    [Obsolete(WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<TResponse> ClientByteStreamingValueTaskAsync<TRequest, TResponse>(
        this in CallContext options,
        CallInvoker invoker, Method<TRequest, TResponse> method, Stream request, string? host = null)
        where TRequest : class
        where TResponse : class
        => new(ClientByteStreamingImplAsync(invoker.AsyncClientStreamingCall(Assert<BytesValue, TResponse>(method), host, options.CallOptions), request, options.Prepare(), options.CancellationToken));

    /// <summary>
    /// Performs a gRPC client-streaming call consuming a byte-stream
    /// </summary>
    [Obsolete(WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask ClientByteStreamingValueTaskAsyncVoid<TRequest, TResponse>(
        this in CallContext options,
        CallInvoker invoker, Method<TRequest, TResponse> method, Stream request, string? host = null)
        where TRequest : class
        where TResponse : class
        => new(ClientByteStreamingImplAsync(invoker.AsyncClientStreamingCall(Assert<BytesValue, TResponse>(method), host, options.CallOptions), request, options.Prepare(), options.CancellationToken));

    private static async Task<TResponse> ClientByteStreamingImplAsync<TResponse>(AsyncClientStreamingCall<BytesValue, TResponse> call, Stream stream, MetadataContext? metadata, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // read from the stream and write to RequestStream
            int size = 512; // start modest and increase

            if (metadata is not null)
            {
                await metadata.SetHeadersAsync(call.ResponseHeadersAsync).ConfigureAwait(false);
            }

            while (true)
            {
                byte[] leased = ArrayPool<byte>.Shared.Rent(size);

                var maxRead = Math.Min(leased.Length, BytesValue.MaxLength);
                var bytes = await stream.ReadAsync(leased, 0, maxRead, cancellationToken).ConfigureAwait(false);
                if (bytes <= 0) // EOF
                {
                    ArrayPool<byte>.Shared.Return(leased);
                    break;
                }
                if (bytes == maxRead)
                {
                    // allow more next time
                    size = Math.Min(size * 2, BytesValue.MaxLength);
                }
                else
                {
                    // allow less next time, down to whatever we read
                    size = Math.Max(bytes, 128);
                }

                var chunk = new BytesValue(leased, bytes, pooled: true);

                await call.RequestStream.WriteAsync(chunk).ConfigureAwait(false);
            }
            await call.RequestStream.CompleteAsync().ConfigureAwait(false);

            var result = await call.ResponseAsync.ConfigureAwait(false);
            metadata?.SetTrailers(call);
            return result;
        }
        catch (RpcException fault)
        {
            metadata?.SetTrailers(fault);
            throw;
        }
        finally
        {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            await stream.DisposeAsync().ConfigureAwait(false);
#else
            stream.Dispose();
#endif
            call.Dispose();
        }
    }



    /// <summary>
    /// Consumes a byte-stream and writes it to a server stream-writer
    /// </summary>
    [Obsolete(WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public static async Task WriteStream(Task<Stream> source, IAsyncStreamWriter<BytesValue> writer, ServerCallContext context, bool writeTrailer)
    {
        try
        {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        await // IDisposable is up-level
#endif
            using var stream = await source;

            // read from the stream and write to writer
            int size = 512; // start modest and increase
            long totalLength = 0;
#if DEBUG
            int debugChunk = 0;
#endif

            while (true)
            {
                byte[] leased = ArrayPool<byte>.Shared.Rent(size);

                var maxRead = Math.Min(leased.Length, BytesValue.MaxLength);
                var bytes = await stream.ReadAsync(leased, 0, maxRead, context.CancellationToken).ConfigureAwait(false);
                if (bytes <= 0) // EOF
                {
                    ArrayPool<byte>.Shared.Return(leased);
                    break;
                }
                if (bytes == maxRead)
                {
                    // allow more next time
                    size = Math.Min(size * 2, BytesValue.MaxLength);
                }
                else
                {
                    // allow less next time, down to whatever we read
                    size = Math.Max(bytes, 128);
                }

                var chunk = new BytesValue(leased, bytes, pooled: true);
#if DEBUG
                context.ResponseTrailers.Add($"pbn_chunk{debugChunk}", bytes);
#endif
                totalLength += bytes;
                await writer.WriteAsync(chunk).ConfigureAwait(false);
            }

            if (writeTrailer)
            {
                context.ResponseTrailers.Add(TrailerStreamLength, totalLength.ToString(CultureInfo.InvariantCulture));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            throw;
        }
    }

    // more idiomatic labels like content-length are reserved, and are not transmitted/received
    internal const string TrailerStreamLength = "stream-length";

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
