using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal;

interface IHandler : IPooled
{
    FrameKind Kind { get; }

    ValueTask PushPayloadAsync(StreamFrame frame, ChannelWriter<StreamFrame> output, ILogger? logger, CancellationToken cancellationToken);
    ValueTask CompleteAsync(CancellationToken cancellationToken);
}

abstract class HandlerBase<TReceive> : IHandler where TReceive : class
{
    protected abstract Marshaller<TReceive> ReceiveMarshaller { get; }

    public abstract ValueTask CompleteAsync(CancellationToken cancellationToken);

    public abstract void Recycle();

    Queue<StreamFrame>? _backlog;
    public ValueTask PushPayloadAsync(StreamFrame frame, ChannelWriter<StreamFrame> output, ILogger? logger, CancellationToken cancellationToken)
    {
        if ((frame.KindFlags & (byte)PayloadFlags.EndItem) == 0)
        {
            (_backlog ??= new Queue<StreamFrame>()).Enqueue(frame);
            return default;
        }
        return PushCompletePayloadAsync(frame, output, logger, cancellationToken);
    }
    private ValueTask PushCompletePayloadAsync(StreamFrame frame, ChannelWriter<StreamFrame> output, ILogger? logger, CancellationToken cancellationToken)
    {
        var backlog = _backlog;
        if (backlog is null || backlog.Count == 0)
        {
            // single frame; simple case
            logger.LogDebug(frame.Length, static (state, _) => $"deserializing request in single buffer, {state} bytes...");
            var ctx = Pool<SingleBufferDeserializationContext>.Get();
            ctx.Initialize(frame.Buffer, frame.Offset, frame.Length);
            var request = ReceiveMarshaller.ContextualDeserializer(ctx);
            ctx.Recycle();
            logger.LogDebug(frame.Length, static (state, _) => $"deserialized {state} bytes; processing request");
            return PushCompletePayloadAsync(frame.RequestId, output, request, logger, cancellationToken);
        }
        else
        {
            // we have multiple frames to marry
            throw new NotImplementedException();
        }
    }

    protected abstract ValueTask PushCompletePayloadAsync(ushort id, ChannelWriter<StreamFrame> output, TReceive value, ILogger? logger, CancellationToken cancellationToken);
    public abstract FrameKind Kind { get; }
}