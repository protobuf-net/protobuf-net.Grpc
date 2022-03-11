using Grpc.Core;

namespace ProtoBuf.Grpc.Lite.Internal.Client;

internal sealed class ClientUnaryHandler<TRequest, TResponse> : ClientHandler<TRequest, TResponse> where TResponse : class where TRequest : class
{

    public override void Recycle()
    {
        var tmp = _tcs;
        _tcs = null;
        tmp?.TrySetCanceled();
        Pool<ClientUnaryHandler<TRequest, TResponse>>.Put(this);
    }
    private TaskCompletionSource<TResponse>? _tcs;

    public override FrameKind Kind => FrameKind.NewUnary;

    public static ClientUnaryHandler<TRequest, TResponse> Get(CancellationToken cancellation)
    {
        var obj = AllowClientRecycling ? Pool<ClientUnaryHandler<TRequest, TResponse>>.Get() : new ClientUnaryHandler<TRequest, TResponse>();
        obj._tcs = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        obj.Register(cancellation);
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

    protected override ValueTask ReceivePayloadAsync(TResponse value, CancellationToken cancellationToken)
    {
        bool success = _tcs!.TrySetResult(value);
        if (success)
        {
            Logger.LogDebug(Id, static (state, _) => $"assigned response for request {state}");
        }
        else
        {
            Logger.LogDebug(Id, static (state, _) => $"unable to assign response for request {state}");
        }
        UnregisterCancellation();
        return default;
    }
}