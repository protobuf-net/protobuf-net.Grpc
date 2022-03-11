using Grpc.Core;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Lite.Internal.Client;

internal interface IClientHandler : IHandler, IDisposable
{
    ushort Id { get; }
    Task<Metadata> ResponseHeadersAsync { get; }

    Status Status { get; }
    Metadata Trailers();
    void Cancel();
}

internal abstract class ClientHandler<TResponse> : HandlerBase<TResponse>, IClientHandler where TResponse : class
{
    CancellationTokenRegistration _ctr;
    public ushort Id { get; private set;  }


    protected const bool AllowClientRecycling = false; // see comments in Dispose()

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        OnDispose(); // make sure we clean up *before* recycling

        // the question here is; can we safely call Recycle()?
        // the problem is that AsyncUnaryCall etc are exposed to the external world, and when disposed: come here;
        // external callers could double-dispose, which is an error, but...

#pragma warning disable CS0162 // Unreachable code detected
        if (AllowClientRecycling) Recycle(); // compiler will remove when possible
#pragma warning restore CS0162 // Unreachable code detected
    }

    protected virtual void OnDispose() { }

    public Task<Metadata> ResponseHeadersAsync => throw new NotImplementedException();

    public Status Status { get; protected set; }

    protected override Marshaller<TResponse> ReceiveMarshaller => _marshaller;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private Marshaller<TResponse> _marshaller; // set in Initialize
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    protected void Initialize(ushort id, Marshaller<TResponse> marshaller, CancellationToken cancellationToken)
    {
        Id = id;
        _marshaller = marshaller;
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ctr = cancellationToken.Register(static state => ((IClientHandler)state!).Cancel(), this);
        }
    }

    public Metadata Trailers() => throw new NotImplementedException();

    public virtual void Reset()
    {
        UnregisterCancellation();
        // not really necessary to reset the rest
    }

    public void UnregisterCancellation()
    {
        var tmp = _ctr;
        _ctr = default;
        tmp.Dispose();
    }

    void IClientHandler.Cancel() => Cancel(_ctr.Token);

    protected abstract void Cancel(CancellationToken cancellationToken);
}
