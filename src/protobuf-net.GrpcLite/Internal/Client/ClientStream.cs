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
}

internal sealed class ClientStream<TRequest, TResponse> : LiteStream<TRequest, TResponse>, IClientStreamWriter<TRequest>, IClientStream where TRequest : class where TResponse : class
{
    void IDisposable.Dispose() { }
    public ClientStream(IMethod method, IFrameConnection output, ILogger? logger, IStreamOwner? owner)
        : base(method, output, owner)
    {
        Logger = logger;

    }
    protected sealed override bool IsClient => true;

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

    Task IAsyncStreamWriter<TRequest>.WriteAsync(TRequest message) => SendAsync(message).AsTask();


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
