using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal.Client;

internal sealed class ClientServerStreamingHandler<TRequest, TResponse> : ClientHandler<TRequest, TResponse>, IAsyncStreamReader<TResponse>, IReceiver<TResponse> where TResponse : class where TRequest : class
{
    private static readonly UnboundedChannelOptions s_ChannelOptions = new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = true,
        AllowSynchronousContinuations = false,
    };

    TResponse _current = default!;
    private Channel<TResponse>? _channel;
    private FrameKind _kind;

    internal static ClientServerStreamingHandler<TRequest, TResponse> Get(FrameKind kind)
    {
        var obj = AllowClientRecycling ? Pool<ClientServerStreamingHandler<TRequest, TResponse>>.Get() : new ClientServerStreamingHandler<TRequest, TResponse>();
        obj._kind = kind;
        obj._channel = Channel.CreateUnbounded<TResponse>(s_ChannelOptions);
        return obj;
    }

    private void CompleteResponseChannel() => _channel?.Writer.TryComplete();


    public override void Recycle()
    {
        CompleteResponseChannel();
        _channel = null;
        _current = default!;
        Pool<ClientServerStreamingHandler<TRequest, TResponse>>.Put(this);
    }
    public override FrameKind Kind => _kind;

    protected override void Cancel(CancellationToken cancellationToken)
        => CompleteResponseChannel();

    protected override ValueTask ReceivePayloadAsync(TResponse value, CancellationToken cancellationToken)
    {
        Logger.LogDebug(Id, static (state, ex) => $"adding item to sequence {state}");
        return _channel!.Writer.WriteAsync(value, cancellationToken);
    }
    public override ValueTask CompleteAsync(CancellationToken cancellationToken)
    {
        CompleteResponseChannel();
        return default;
    }

    Task<bool> IAsyncStreamReader<TResponse>.MoveNext(CancellationToken cancellationToken)
        => MoveNextAndCapture(_channel!.Reader, this, cancellationToken);

    void IReceiver<TResponse>.Receive(TResponse value) => _current = value;


    TResponse IAsyncStreamReader<TResponse>.Current => _current!; // if you call it at a time other than after MoveNext(): that's on you
}
