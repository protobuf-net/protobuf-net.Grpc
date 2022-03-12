using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal;

interface IHandler : IPooled
{
    ushort Id { get; }
    FrameKind Kind { get; }
    ValueTask ReceivePayloadAsync(Frame frame, CancellationToken cancellationToken);
    ValueTask CompleteAsync(CancellationToken cancellationToken);
    ushort NextSequenceId();
}

interface IReceiver<T>
{
    void Receive(T value);
}

abstract class HandlerBase<TSend, TReceive> : IHandler where TSend : class where TReceive : class
{
    private ushort _id;
    private ILogger? _logger;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    // these are all set via initialize
    private IMethod _method;
    ChannelWriter<Frame> _output;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public ushort Id => _id;
    private int _sequenceId;
    public ushort NextSequenceId() => Utilities.IncrementToUInt32(ref _sequenceId);
    protected ILogger? Logger => _logger;
    protected IMethod Method
    {
        get => _method;
        set => _method = value;
    }
    protected ChannelWriter<Frame> Output => _output;

    public virtual void Initialize(ushort id, ChannelWriter<Frame> output, ILogger? logger)
    {
        _output = output;
        _logger = logger;
        _id = id;
        _sequenceId = ushort.MaxValue; // so first is zero
    }

    protected abstract Action<TSend, SerializationContext> Serializer { get; }
    protected abstract Func<DeserializationContext, TReceive> Deserializer { get; }

    public abstract ValueTask CompleteAsync(CancellationToken cancellationToken);

    public abstract void Recycle();

    Queue<Frame>? _backlog;
    public ValueTask ReceivePayloadAsync(Frame frame, CancellationToken cancellationToken)
    {
        if ((frame.KindFlags & (byte)PayloadFlags.EndItem) == 0)
        {
            (_backlog ??= new Queue<Frame>()).Enqueue(frame);
            return default;
        }
        return ReceiveCompletePayloadAsync(frame, cancellationToken);
    }
    private ValueTask ReceiveCompletePayloadAsync(Frame frame, CancellationToken cancellationToken)
    {
        var backlog = _backlog;
        if (backlog is null || backlog.Count == 0)
        {
            // single frame; simple case
            _logger.LogDebug(frame.Length, static (state, _) => $"deserializing request in single buffer, {state} bytes...");
            var ctx = Pool<SingleBufferDeserializationContext>.Get();
            ctx.Initialize(frame.Buffer, frame.Offset, frame.Length);
            var request = Deserializer(ctx);
            ctx.Recycle();
            _logger.LogDebug(frame.Length, static (state, _) => $"deserialized {state} bytes; processing request");
            return ReceivePayloadAsync(request, cancellationToken);
        }
        else
        {
            // we have multiple frames to marry
            throw new NotImplementedException();
        }
    }


    public ValueTask SendInitializeAsync(string? host, CallOptions options)
        => Output.WriteAsync(Frame.GetInitializeFrame(Kind, Id, NextSequenceId(), Method.FullName, host), options.CancellationToken);

    internal async Task SendSingleAsync(string? host, CallOptions options, TSend request)
    {
        await SendInitializeAsync(host, options);
        await SendAsync(request, true, options.CancellationToken);
    }

    public async ValueTask SendAsync(TSend value, bool isLastElement, CancellationToken cancellationToken)
    {
        FrameSerializationContext? serializationContext = null;
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            serializationContext = Pool<FrameSerializationContext>.Get();
            Serializer(value, serializationContext);
            await serializationContext.WritePayloadAsync(Output, this, isLastElement, cancellationToken);
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
    public abstract FrameKind Kind { get; }
}