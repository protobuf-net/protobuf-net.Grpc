using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal;

interface IHandler : IPooled
{
    ushort Id { get; }
    FrameKind Kind { get; }
    ValueTask ReceivePayloadAsync(StreamFrame frame, CancellationToken cancellationToken);
    ValueTask CompleteAsync(CancellationToken cancellationToken);
}

abstract class HandlerBase<TSend, TReceive> : IHandler where TSend : class where TReceive : class
{
    private ushort _id;
    private ILogger? _logger;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    // these are all set via initialize
    private IMethod _method;
    ChannelWriter<StreamFrame> _output;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public ushort Id => _id;
    protected ILogger? Logger => _logger;
    protected IMethod Method
    {
        get => _method;
        set => _method = value;
    }
    protected ChannelWriter<StreamFrame> Output => _output;

    public virtual void Initialize(ushort id, ChannelWriter<StreamFrame> output, ILogger? logger)
    {
        _output = output;
        _logger = logger;
        _id = id;
    }

    protected abstract Action<TSend, SerializationContext> Serializer { get; }
    protected abstract Func<DeserializationContext, TReceive> Deserializer { get; }

    public abstract ValueTask CompleteAsync(CancellationToken cancellationToken);

    public abstract void Recycle();

    Queue<StreamFrame>? _backlog;
    public ValueTask ReceivePayloadAsync(StreamFrame frame, CancellationToken cancellationToken)
    {
        if ((frame.KindFlags & (byte)PayloadFlags.EndItem) == 0)
        {
            (_backlog ??= new Queue<StreamFrame>()).Enqueue(frame);
            return default;
        }
        return ReceiveCompletePayloadAsync(frame, cancellationToken);
    }
    private ValueTask ReceiveCompletePayloadAsync(StreamFrame frame, CancellationToken cancellationToken)
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
        => Output.WriteAsync(StreamFrame.GetInitializeFrame(FrameKind.NewUnary, Id, Method.FullName, host), options.CancellationToken);

    internal async Task SendSingleAsync(string? host, CallOptions options, TSend request)
    {
        await SendInitializeAsync(host, options);
        await SendAsync(request, true, options.CancellationToken);
    }

    public async ValueTask SendAsync(TSend value, bool isLastElement, CancellationToken cancellationToken)
    {
        StreamSerializationContext? serializationContext = null;
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            serializationContext = Pool<StreamSerializationContext>.Get();
            Serializer(value, serializationContext);
            await serializationContext.WritePayloadAsync(Output, Id, isLastElement, cancellationToken);
        }
        finally
        {
            serializationContext?.Recycle();
        }
    }


    protected abstract ValueTask ReceivePayloadAsync(TReceive value, CancellationToken cancellationToken);
    public abstract FrameKind Kind { get; }
}