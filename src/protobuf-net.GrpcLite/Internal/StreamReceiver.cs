using Grpc.Core;

namespace ProtoBuf.Grpc.Lite.Internal;

internal interface IStreamReceiver : IDisposable
{
    ushort Id { get; }
    Task<Metadata> ResponseHeadersAsync { get; }

    Status Status { get; }
    Metadata Trailers();
    void Cancel();
}

internal abstract class Receiver<TResponse> : IStreamReceiver
{
    CancellationTokenRegistration _ctr;
    public ushort Id { get; }

    public Task<Metadata> ResponseHeadersAsync => throw new NotImplementedException();

    public Status Status { get; protected set; }

    private readonly Marshaller<TResponse> _marshaller;

    protected Receiver(ushort id, Marshaller<TResponse> marshaller, CancellationToken cancellationToken)
    {
        Id = id;
        _marshaller = marshaller;

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
internal sealed class UnaryStreamReceiver<TResponse> : Receiver<TResponse>
{
    private readonly TaskCompletionSource<TResponse> _tcs;

    public UnaryStreamReceiver(ushort id, Marshaller<TResponse> responseMarshaller, CancellationToken cancellationToken)
        : base(id, responseMarshaller, cancellationToken)
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
}
