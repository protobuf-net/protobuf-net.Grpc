using Grpc.Core;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal.Server;

internal sealed class ServerDuplexHandler<TRequest, TResponse> : ServerHandler<TRequest, TResponse>, IAsyncStreamReader<TRequest>, IReceiver<TRequest> where TResponse : class where TRequest : class
{
    public override FrameKind Kind => FrameKind.NewDuplex;

    private DuplexStreamingServerMethod<TRequest, TResponse>? _handler;
    private Channel<TRequest>? _requests;

    private static readonly UnboundedChannelOptions s_ChannelOptions = new UnboundedChannelOptions
    {
        AllowSynchronousContinuations = false,
        SingleReader = true,
        SingleWriter = false,
    };
    public static ServerDuplexHandler<TRequest, TResponse> Get(Method<TRequest, TResponse> method, DuplexStreamingServerMethod<TRequest, TResponse> handler)
    {
        var obj = Pool<ServerDuplexHandler<TRequest, TResponse>>.Get();
        obj.Method = method;
        obj._handler = handler;
        obj._requests = Channel.CreateUnbounded<TRequest>(s_ChannelOptions);
        return obj;
    }

    public override void Recycle()
    {
        _requests?.Writer.TryComplete();
        _requests = null;
        // not really necessary to reset marshaller/method/handler; they'll be alive globally
        Pool<ServerDuplexHandler<TRequest, TResponse>>.Put(this);
    }

    public override ValueTask CompleteAsync(CancellationToken cancellationToken)
    {
        _requests?.Writer.TryComplete();
        return default;
    }

    protected override ValueTask ReceivePayloadAsync(TRequest value, CancellationToken cancellationToken)
        => _requests!.Writer.WriteAsync(value, cancellationToken);

    protected override Task InvokeServerMethod(ServerCallContext context)
        => _handler!(this, this, context);

    private TRequest? _current;
    TRequest IAsyncStreamReader<TRequest>.Current => _current!;
    void IReceiver<TRequest>.Receive(TRequest value) => _current = value;

    Task<bool> IAsyncStreamReader<TRequest>.MoveNext(CancellationToken cancellationToken)
        => MoveNextAndCapture(_requests!.Reader, this, cancellationToken);
}