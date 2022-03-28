using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal.Client;

internal interface IClientStream : IStream, IDisposable
{
    Task<Metadata> ResponseHeadersAsync { get; }

    Status Status { get; }
    Metadata Trailers();
}

internal sealed class ClientStream<TRequest, TResponse> : LiteStream<TRequest, TResponse>, IClientStreamWriter<TRequest>, IClientStream where TRequest : class where TResponse : class
{
    public override void Dispose()
    {
        base.Dispose();
        var tmp = _ctr;
        _ctr = default;
        tmp.SafeDispose();
    }

    private CancellationTokenRegistration _ctr;
    internal override CancellationTokenRegistration RegisterForCancellation(CancellationToken streamSpecificCancellation, DateTime? deadline)
        => _ctr = base.RegisterForCancellation(streamSpecificCancellation, deadline);

    public ClientStream(IMethod method, IConnection owner, ILogger? logger)
        : base(method, owner)
    {
        Logger = logger;
    }
    protected sealed override bool IsClient => true;

    Task IClientStreamWriter<TRequest>.CompleteAsync()
        => SendTrailerAsync(null, null, FrameWriteFlags.None).AsTask();

    Task IAsyncStreamWriter<TRequest>.WriteAsync(TRequest message) => SendAsync(message, WriterFlags).AsTask();

    protected override void OnCancel() => TrySendCancellation();

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


    public Metadata Trailers()
    {
        Logger.ThrowNotImplemented();
        return Metadata.Empty;
    }
}
