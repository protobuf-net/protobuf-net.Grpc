//using Grpc.Core;
//using System.Threading.Channels;

//namespace ProtoBuf.Grpc.Lite.Internal.Server;

//internal sealed class ServerDuplexHandler<TRequest, TResponse> : ServerStream<TRequest, TResponse>, IAsyncStreamReader<TRequest> where TResponse : class where TRequest : class
//{

//    private DuplexStreamingServerMethod<TRequest, TResponse>? _handler;

//    private Channel<TRequest> _requests = null!; // TODO: pretty sure we can replace this with a MRVTS

//    private static readonly UnboundedChannelOptions s_ChannelOptions = new UnboundedChannelOptions
//    {
//        AllowSynchronousContinuations = true,
//        SingleReader = true,
//        SingleWriter = true,
//    };
//    public static ServerDuplexHandler<TRequest, TResponse> Get(Method<TRequest, TResponse> method, DuplexStreamingServerMethod<TRequest, TResponse> handler)
//    {
//        var obj = Pool<ServerDuplexHandler<TRequest, TResponse>>.Get();
//        obj.Method = method;
//        obj._handler = handler;
//        obj._requests = Channel.CreateUnbounded<TRequest>(s_ChannelOptions);
//        return obj;
//    }
//    public override void Recycle()
//    {
//        _requests = null!;
//        // not really necessary to reset marshaller/method/handler; they'll be alive globally
//        Pool<ServerDuplexHandler<TRequest, TResponse>>.Put(this);
//    }

//    protected override Task InvokeServerMethod(ServerCallContext context)
//        => _handler!(this, this, context);

//    TRequest IAsyncStreamReader<TRequest>.Current => _current!;


//    protected override ValueTask OnPayloadAsync(TRequest value)
//        => _requests!.Writer.WriteAsync(value, StreamCancellation);

//    private TRequest? _current;
//    protected override ValueTask OnPayloadEnd()
//    {
//        _requests!.Writer.TryComplete();
//        return default;
//    }

//    Task<bool> IAsyncStreamReader<TRequest>.MoveNext(CancellationToken cancellationToken)
//    {
//        if (_requests!.Reader.TryRead(out _current))
//        {
//            return Utilities.AsyncTrue;
//        }
//        if (!_requests.Reader.Completion.IsCompletedSuccessfully)
//        {
//            // it is completed; have another try, but we're sure either way now
//            return _requests!.Reader.TryRead(out _current) ? Utilities.AsyncTrue : Utilities.AsyncFalse;
//        }
//        return Awaited(this, cancellationToken);

//        static async Task<bool> Awaited(ServerDuplexHandler<TRequest, TResponse> handler, CancellationToken cancellationToken)
//        {
//            return await handler!._requests.Reader.WaitToReadAsync(cancellationToken)
//                && handler!._requests.Reader.TryRead(out handler!._current);
//        }
//    }
//}