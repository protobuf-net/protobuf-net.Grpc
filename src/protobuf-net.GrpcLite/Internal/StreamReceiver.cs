using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal;

internal interface IStreamReceiver : IDisposable, IHandler
{
    ushort Id { get; }
    Task<Metadata> ResponseHeadersAsync { get; }

    Status Status { get; }
    Metadata Trailers();
    void Cancel();
}

internal abstract class Receiver<TResponse> : HandlerBase<TResponse>, IStreamReceiver where TResponse : class
{
    CancellationTokenRegistration _ctr;
    public ushort Id { get; }

    public Task<Metadata> ResponseHeadersAsync => throw new NotImplementedException();

    public Status Status { get; protected set; }

    protected Receiver(ushort id, Marshaller<TResponse> marshaller, CancellationToken cancellationToken)
        : base(marshaller)
    {
        Id = id;

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ctr = cancellationToken.Register(static state => ((IStreamReceiver)state!).Cancel(), this);
        }
    }

    public Metadata Trailers() => throw new NotImplementedException();

    void IDisposable.Dispose()
    {
        UnregisterCancellation();
    }

    public void UnregisterCancellation()
    {
        var tmp = _ctr;
        _ctr = default;
        tmp.Dispose();
    }

    void IStreamReceiver.Cancel() => Cancel(_ctr.Token);

    protected abstract void Cancel(CancellationToken cancellationToken);
}
internal sealed class UnaryStreamReceiver<TResponse> : Receiver<TResponse> where TResponse : class
{
    private readonly TaskCompletionSource<TResponse> _tcs;

    public override FrameKind Kind => FrameKind.NewUnary;

    public UnaryStreamReceiver(ushort id, Marshaller<TResponse> marshaller, CancellationToken cancellationToken)
        : base(id, marshaller, cancellationToken)
    {
        _tcs = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Task<TResponse> ResponseAsync => _tcs.Task;

    internal void Fault(string message, Exception exception)
    {
        Status = new(StatusCode.Internal, message, exception);
        _tcs.TrySetException(exception);
    }

    protected override void Cancel(CancellationToken cancellationToken)
        => _tcs.TrySetCanceled(cancellationToken);

    public override ValueTask CompleteAsync(CancellationToken cancellationToken)
        => throw new NotImplementedException();

    protected override ValueTask PushCompletePayloadAsync(ushort id, ChannelWriter<StreamFrame> output, TResponse value, ILogger? logger, CancellationToken cancellationToken)
    {
        bool success = _tcs.TrySetResult(value);
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
