//using Grpc.Core;

//namespace ProtoBuf.Grpc.Lite.Internal.Client;

//internal sealed class ClientUnaryHandler<TRequest, TResponse> : ClientHandler<TRequest, TResponse> where TResponse : class where TRequest : class
//{

//    public override void Recycle()
//    {
//        UnregisterCancellation();
//        var tmp = _tcs;
//        _tcs = null;
//        tmp?.TrySetCanceled();
//        Pool<ClientUnaryHandler<TRequest, TResponse>>.Put(this);
//    }
//    private TaskCompletionSource<TResponse>? _tcs;

//    public static ClientUnaryHandler<TRequest, TResponse> Get(CancellationToken cancellation)
//    {
//        var obj = AllowClientRecycling ? Pool<ClientUnaryHandler<TRequest, TResponse>>.Get() : new ClientUnaryHandler<TRequest, TResponse>();
//        obj._tcs = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
//        obj.Register(cancellation);
//        return obj;
//    }

//    public Task<TResponse> ResponseAsync => _tcs!.Task;

//    internal void Fault(string message, Exception exception)
//    {
//        Status = new(StatusCode.Internal, message, exception);
//        _tcs?.TrySetException(exception);
//    }

//    protected override void Cancel(CancellationToken cancellationToken)
//        => _tcs?.TrySetCanceled(cancellationToken);

//    protected override ValueTask OnPayloadAsync(TResponse value)
//    {
//        _tcs?.TrySetResult(value);
//        return default;
//    }
//    protected override ValueTask OnPayloadEnd()
//    {
//        _tcs?.TrySetException(NoPayload()); // if we didn't already get a payload, something is bad
//        return default;
//        static Exception NoPayload()
//        {
//            try
//            {
//                throw new InvalidOperationException("No payload received");
//            }
//            catch (Exception ex)
//            {
//                return ex; // now with a stack-trace
//            }
//        }
//    }

//    //protected override ValueTask ReceivePayloadAsync(TResponse value, CancellationToken cancellationToken)
//    //{
//    //    bool success = _tcs!.TrySetResult(value);
//    //    if (success)
//    //    {
//    //        Logger.LogDebug(StreamId, static (state, _) => $"assigned response for request {state}");
//    //    }
//    //    else
//    //    {
//    //        Logger.LogDebug(StreamId, static (state, _) => $"unable to assign response for request {state}");
//    //    }
//    //    UnregisterCancellation();
//    //    return default;
//    //}
//}