using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal.Client;

internal sealed class ClientServerStreamingHandler<TRequest, TResponse> : ClientHandler<TRequest, TResponse>, IAsyncStreamReader<TResponse> where TResponse : class where TRequest : class
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
    {
        // try to get something the cheap way
        return _channel!.Reader.TryRead(out var _current) ? Utilities.AsyncTrue : SlowMoveNext(this, cancellationToken);

        static Task<bool> SlowMoveNext(ClientServerStreamingHandler<TRequest, TResponse> handler, CancellationToken cancellationToken)
        {
            do
            {
                var waitToReadAsync = handler._channel!.Reader.WaitToReadAsync(cancellationToken);
                if (!waitToReadAsync.IsCompletedSuccessfully)
                {   // nothing readily available, and WaitToReadAsync returned incomplete; we'll
                    // need to switch to async here
                    return AwaitToRead(handler, waitToReadAsync, cancellationToken);
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
            }
            while (!handler._channel.Reader.TryRead(out handler._current!));

            // we got success on the not-first attempt; I'll take the W
            return Utilities.AsyncTrue;
        }

        static async Task<bool> AwaitToRead(ClientServerStreamingHandler<TRequest, TResponse> handler, ValueTask<bool> waitToReadAsync, CancellationToken cancellationToken)
        {
            while (await waitToReadAsync)
            {
                if (handler._channel!.Reader.TryRead(out handler._current!)) return true;
                waitToReadAsync = handler._channel!.Reader.WaitToReadAsync(cancellationToken);
            }
            return false;
        }
    }
    
    TResponse IAsyncStreamReader<TResponse>.Current => _current!; // if you call it at a time other than after MoveNext(): that's on you
}
