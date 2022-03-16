using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Lite.Internal.Client;

internal interface IClientHandler : IStream, IDisposable
{
    new ushort Id { get; set; } // adds setter
    void Initialize(IMethod method, IFrameConnection output, ILogger? logger);
    Task<Metadata> ResponseHeadersAsync { get; }

    Status Status { get; }
    Metadata Trailers();
    void Cancel();
}

internal abstract class ClientHandler<TRequest, TResponse> : HandlerBase<TRequest, TResponse>, IClientStreamWriter<TRequest>, IClientHandler where TRequest : class where TResponse : class
{
    protected sealed override bool IsClient => true;
    public void Initialize(IMethod method, IFrameConnection output, ILogger? logger)
    {
        Initialize(Id, output, logger);
        Method = method;
    }

    CancellationTokenRegistration _ctr;

    private WriteOptions? _writeOptions;
    WriteOptions IAsyncStreamWriter<TRequest>.WriteOptions
    {
        get => _writeOptions!;
        set => _writeOptions = value;
    }

    Task IClientStreamWriter<TRequest>.CompleteAsync()
    {
        //CompleteResponseChannel();
        return Task.CompletedTask;
    }

    Task IAsyncStreamWriter<TRequest>.WriteAsync(TRequest message)
        => SendAsync(message, PayloadFlags.None, default).AsTask();

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

    protected override Action<TRequest, SerializationContext> Serializer => TypedMethod.RequestMarshaller.ContextualSerializer;
    protected override Func<DeserializationContext, TResponse> Deserializer => TypedMethod.ResponseMarshaller.ContextualDeserializer;

    protected Method<TRequest, TResponse> TypedMethod => Unsafe.As<Method<TRequest, TResponse>>(Method);

    protected void Register(CancellationToken cancellationToken)
    {
        _writeOptions = WriteOptions.Default;
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
