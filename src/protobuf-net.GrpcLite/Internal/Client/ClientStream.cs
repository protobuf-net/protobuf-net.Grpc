using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Lite.Internal.Client;

internal interface IClientStream : IStream, IDisposable
{
    Task<Metadata> ResponseHeadersAsync { get; }

    Status Status { get; }
    Metadata Trailers();
    void Cancel();
}

internal sealed class ClientStream<TRequest, TResponse> : LiteStream<TRequest, TResponse>, IClientStreamWriter<TRequest>, IClientStream where TRequest : class where TResponse : class
{
    void IDisposable.Dispose() { }
    protected override ValueTask OnPayloadAsync(TResponse value)
    {
        Logger.ThrowNotImplemented();
        return default;
    }
    public ClientStream(IMethod method, IFrameConnection output, ILogger? logger)
        : base(method, output)
    {
        Logger = logger;

    }
    protected sealed override bool IsClient => true;

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

    protected override ValueTask ExecuteAsync()
        => throw new NotSupportedException("client execution is not supported");

    Task IAsyncStreamWriter<TRequest>.WriteAsync(TRequest message)
        => SendAsync(message, PayloadFlags.None, default).AsTask();


    public Task<Metadata> ResponseHeadersAsync
    {
        get
        {
            Logger.ThrowNotImplemented();
            return Task.FromResult(Metadata.Empty);
        }
    }

    public Status Status { get; private set; }

    protected override Action<TRequest, SerializationContext> Serializer => TypedMethod.RequestMarshaller.ContextualSerializer;
    protected override Func<DeserializationContext, TResponse> Deserializer => TypedMethod.ResponseMarshaller.ContextualDeserializer;
    private Method<TRequest, TResponse> TypedMethod => Unsafe.As<Method<TRequest, TResponse>>(Method);

    private void Register(CancellationToken cancellationToken)
    {
        _writeOptions = WriteOptions.Default;
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ctr = cancellationToken.Register(static state => ((IClientStream)state!).Cancel(), this);
        }
    }

    public Metadata Trailers()
    {
        Logger.ThrowNotImplemented();
        return Metadata.Empty;
    }

    public void UnregisterCancellation()
    {
        var tmp = _ctr;
        _ctr = default;
        tmp.Dispose();
    }

    void IClientStream.Cancel() => Cancel(_ctr.Token);

    public void Cancel(CancellationToken cancellationToken)
    {
        Logger.ThrowNotImplemented();
    }
}
