using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Buffers;
using System.Text;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal;

interface IHandler : IPooled
{
    ushort StreamId { get; }
    ValueTask ReceivePayloadAsync(Frame frame, CancellationToken cancellationToken);
    ValueTask CompleteAsync(CancellationToken cancellationToken);
    ushort NextSequenceId();
    MethodType MethodType { get; }
}

interface IReceiver<T>
{
    void Receive(T value);
}

abstract class HandlerBase<TSend, TReceive> : IHandler where TSend : class where TReceive : class
{
    protected FrameBufferManager BufferManager => FrameBufferManager.Shared;

    private ushort _streamId;
    private ILogger? _logger;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    // these are all set via initialize
    private IMethod _method;
    IFrameConnection _output;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public ushort StreamId
    {
        get => _streamId;
        set => _streamId = value;
    }
    private int _sequenceId;

    protected abstract bool IsClient { get; }
    public ushort NextSequenceId()
    {
        // MSB bit is always off for clients, and always on for servers
        var next = Utilities.IncrementToUInt32(ref _sequenceId) & 0x7FFF;
        return (ushort)(IsClient ? next : next | 0x8000);
    }
    protected ILogger? Logger => _logger;
    protected IMethod Method
    {
        get => _method;
        set => _method = value;
    }
    protected IFrameConnection Output => _output;

    public virtual void Initialize(ushort streamId, IFrameConnection output, ILogger? logger)
    {
        _output = output;
        _logger = logger;
        _streamId = streamId;
        _sequenceId = ushort.MaxValue; // so first is zero
    }

    protected abstract Action<TSend, SerializationContext> Serializer { get; }
    protected abstract Func<DeserializationContext, TReceive> Deserializer { get; }

    public abstract ValueTask CompleteAsync(CancellationToken cancellationToken);

    public abstract void Recycle();

    Queue<ReadOnlyMemory<byte>>? _backlog;
    private void AddBacklog(in ReadOnlyMemory<byte> payload)
    {
        if (!payload.IsEmpty)
        {
            (_backlog ??= new Queue<ReadOnlyMemory<byte>>()).Enqueue(payload);
        }
    }

    private ReadOnlySequence<byte> Flush(in ReadOnlyMemory<byte> final)
    {
        var backlog = _backlog;
        if (backlog is null || backlog.Count == 0)
        {   // single-frame payload
            return final.IsEmpty ? default : new ReadOnlySequence<byte>(final);
        }
        backlog.Enqueue(final);
        throw new NotImplementedException("build the chain");
    }
    public ValueTask ReceivePayloadAsync(Frame frame, CancellationToken cancellationToken)
    {
        var header = frame.GetHeader();
        
        var payload = frame.GetPayload();
        bool isComplete = (header.KindFlags & (byte)PayloadFlags.EndItem) != 0;

        if (isComplete)
        {
            return ReceiveCompletePayloadAsync(Flush(payload), cancellationToken);
        }
        else
        {
            AddBacklog(payload);
            return default;
        }
    }
    private ValueTask ReceiveCompletePayloadAsync(ReadOnlySequence<byte> payload, CancellationToken cancellationToken)
    {
        _logger.LogDebug(payload.Length, static (state, _) => $"deserializing payload, {state} bytes...");
        var ctx = Pool<SingleBufferDeserializationContext>.Get();
        ctx.Initialize(payload);
        var request = Deserializer(ctx);
        ctx.Recycle();
        _logger.LogDebug(payload.Length, static (state, _) => $"deserialized {state} bytes; processing request");
        return ReceivePayloadAsync(request, cancellationToken);
    }


    public ValueTask SendInitializeAsync(string? host, CallOptions options)
        => Output.WriteAsync(GetInitializeFrame(host), options.CancellationToken);

    internal Frame GetInitializeFrame(string? host)
    {
        var fullName = Method.FullName;
        if (string.IsNullOrEmpty(fullName)) ThrowMissingMethod();
        if (!string.IsNullOrEmpty(host)) ThrowNotSupported(); // in future: delimit?
        var length = Encoding.UTF8.GetByteCount(fullName);
        if (length > FrameHeader.MaxPayloadSize) ThrowMethodTooLarge(length);

        var slab = BufferManager.Rent(length);
        try
        {
            var header = new FrameHeader(FrameKind.NewStream, 0, StreamId, NextSequenceId(), (ushort)length);
            slab.Advance(Encoding.UTF8.GetBytes(fullName, slab.ActiveBuffer.Span));
            return slab.CreateFrameAndInvalidate(header, updateHeaderLength: false); // this will validate etc
        }
        finally
        {
            slab?.Return();
        }

        static void ThrowMissingMethod() => throw new ArgumentOutOfRangeException(nameof(fullName), "No method name was specified");
        static void ThrowNotSupported() => throw new ArgumentOutOfRangeException(nameof(host), "Non-empty hosts are not currently supported");
        static void ThrowMethodTooLarge(int length) => throw new InvalidOperationException($"The method name is too large at {length} bytes");
    }

    internal async Task SendSingleAsync(string? host, CallOptions options, TSend request)
    {
        try
        {
            await SendInitializeAsync(host, options);
            await SendAsync(request, PayloadFlags.FinalItem, options.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
        }
    }

    public async ValueTask SendAsync(TSend value, PayloadFlags flags, CancellationToken cancellationToken)
    {
        PayloadFrameSerializationContext? serializationContext = null;
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            serializationContext = PayloadFrameSerializationContext.Get(this, BufferManager, StreamId, flags);
            Serializer(value, serializationContext);
            await serializationContext.WritePayloadAsync(Output, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            throw;
        }
        finally
        {
            serializationContext?.Recycle();
        }
    }

    public static Task<bool> MoveNextAndCapture(ChannelReader<TReceive> source, IReceiver<TReceive> destination, CancellationToken cancellationToken)
    {
        // try to get something the cheap way
        if (source.TryRead(out var value))
        {
            destination.Receive(value);
            return Utilities.AsyncTrue;
        }
        return SlowMoveNext(source, destination, cancellationToken);

        static Task<bool> SlowMoveNext(ChannelReader<TReceive> source, IReceiver<TReceive> destination, CancellationToken cancellationToken)
        {
            TReceive value;
            do
            {
                var waitToReadAsync = source.WaitToReadAsync(cancellationToken);
                if (!waitToReadAsync.IsCompletedSuccessfully)
                {   // nothing readily available, and WaitToReadAsync returned incomplete; we'll
                    // need to switch to async here
                    return AwaitToRead(source, destination, waitToReadAsync, cancellationToken);
                }

                var canHaveMore = waitToReadAsync.Result;
                if (!canHaveMore)
                {
                    // nothing readily available, and WaitToReadAsync return false synchronously;
                    // we're all done!
                    return Utilities.AsyncFalse;
                }
                // otherwise, there *should* be work, but we might be having a race right; try again,
                // until we succeed

                // try again
            }
            while (!source.TryRead(out value!));
            destination.Receive(value);
            // we got success on the not-first attempt; I'll take the W
            return Utilities.AsyncTrue;
        }

        static async Task<bool> AwaitToRead(ChannelReader<TReceive> source, IReceiver<TReceive> destination, ValueTask<bool> waitToReadAsync, CancellationToken cancellationToken)
        {
            while (await waitToReadAsync)
            {
                if (source.TryRead(out var value))
                {
                    destination.Receive(value);
                    return true;
                }
                waitToReadAsync = source.WaitToReadAsync(cancellationToken);
            }
            return false;
        }
    }

    protected abstract ValueTask ReceivePayloadAsync(TReceive value, CancellationToken cancellationToken);
    MethodType IHandler.MethodType => _method!.Type;
}