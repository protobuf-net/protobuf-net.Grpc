using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal.Client;

internal sealed class ClientUnaryHandler<TResponse> : ClientHandler<TResponse> where TResponse : class
{

    public override void Recycle()
    {
        var tmp = _tcs;
        _tcs = null;
        tmp?.TrySetCanceled();
        Pool<ClientUnaryHandler<TResponse>>.Put(this);
    }
    private TaskCompletionSource<TResponse>? _tcs;

    public override FrameKind Kind => FrameKind.NewUnary;

    public static ClientUnaryHandler<TResponse> Get(ushort id, Marshaller<TResponse> marshaller, CancellationToken cancellationToken)
    {
        var obj = AllowClientRecycling ? Pool<ClientUnaryHandler<TResponse>>.Get() : new ClientUnaryHandler<TResponse>();
        obj.Initialize(id, marshaller, cancellationToken);
        obj._tcs = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        return obj;
    }

    public Task<TResponse> ResponseAsync => _tcs!.Task;

    internal void Fault(string message, Exception exception)
    {
        Status = new(StatusCode.Internal, message, exception);
        _tcs!.TrySetException(exception);
    }

    protected override void Cancel(CancellationToken cancellationToken)
        => _tcs!.TrySetCanceled(cancellationToken);

    public override ValueTask CompleteAsync(CancellationToken cancellationToken)
        => throw new NotImplementedException();

    protected override ValueTask PushCompletePayloadAsync(ushort id, ChannelWriter<StreamFrame> output, TResponse value, ILogger? logger, CancellationToken cancellationToken)
    {
        bool success = _tcs!.TrySetResult(value);
        if (success)
        {
            logger.LogDebug(id, static (state, _) => $"assigned response for request {state}");
        }
        else
        {
            logger.LogDebug(id, static (state, _) => $"unable to assign response for request {state}");
        }
        UnregisterCancellation();
        return default;
    }
}