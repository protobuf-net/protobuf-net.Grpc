using Grpc.Core;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal;

internal sealed class StreamCallInvoker : CallInvoker
{
    private Channel<StreamFrame> _outbound;
    private ConcurrentDictionary<ushort, IStreamReceiver> _receivers = new();
    private uint _nextId = ushort.MaxValue; // so that our first id is zero
    private ushort NextId()
    {
        while (true)
        {
            var id = Interlocked.Increment(ref _nextId);
            if (id <= ushort.MaxValue) return (ushort)id; // in-range; that'll do

            // try to swap to zero; if we win: we are become zero
            if (Interlocked.CompareExchange(ref _nextId, 0, id) == id) return 0;

            // otherwise, redo from start
        }
    }

    public StreamCallInvoker(Channel<StreamFrame> outbound)
    {
        this._outbound = outbound;
    }

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        => SimpleUnary(method, host, options, request).GetAwaiter().GetResult();

    async Task<TResponse> SimpleUnary<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        where TRequest : class
        where TResponse : class
    {
        using var call = AsyncUnaryCall(method, host, options, request);
        return await call.ResponseAsync;
    }

    static readonly Func<object, Task<Metadata>> responseHeadersAsync = static state => ((IStreamReceiver)state).ResponseHeadersAsync;
    static readonly Func<object, Status> getStatus = static state => ((IStreamReceiver)state).Status;
    static readonly Func<object, Metadata> getTrailers = static state => ((IStreamReceiver)state).Trailers();
    static readonly Action<object> dispose = static state => ((IStreamReceiver)state).Dispose();
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        var receiver = new UnaryStreamReceiver<TResponse>(NextId(), method.ResponseMarshaller, options.CancellationToken);
        _ = WriteAsync(receiver, method, host, options, request);
        return new AsyncUnaryCall<TResponse>(receiver.ResponseAsync, responseHeadersAsync, getStatus, getTrailers, dispose, receiver);
    }

    private void AddReceiver(IStreamReceiver receiver)
    {
        if (receiver is null) ThrowNull();
        if (!_receivers.TryAdd(receiver!.Id, receiver)) ThrowDuplicate(receiver.Id);

        static void ThrowNull() => throw new ArgumentNullException(nameof(receiver));
        static void ThrowDuplicate(ushort id) => throw new ArgumentException($"Duplicate receiver key: {id}");
    }

    private async Task WriteAsync<TResponse, TRequest>(UnaryStreamReceiver<TResponse> receiver, Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        where TResponse : class
        where TRequest : class
    {
        StreamSerializationContext? serializationContext = null;
        bool complete = false;
        options.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            AddReceiver(receiver);
            await _outbound.Writer.WriteAsync(StreamFrame.GetInitializeFrame(FrameKind.NewUnary, receiver.Id, method.FullName, host));
            serializationContext = StreamSerializationContext.Get();
            method.RequestMarshaller.ContextualSerializer(request, serializationContext);
            await serializationContext.WriteAsync(_outbound.Writer, receiver.Id, options.CancellationToken);
            complete = true;
        }
        catch (Exception ex)
        {
            if (receiver is not null)
            {
                _receivers.TryRemove(receiver.Id, out _);
                receiver?.Fault("Error writing message", ex);
            }
        }
        finally
        {
            if (!complete) 
            {
                await _outbound.Writer.WriteAsync(new StreamFrame(FrameKind.Cancel, receiver.Id, 0));
            }
            serializationContext?.Recycle();
        }
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
    {
        throw new NotImplementedException();
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
    {
        throw new NotImplementedException();
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        throw new NotImplementedException();
    }
}
