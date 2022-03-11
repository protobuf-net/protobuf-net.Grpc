using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ProtoBuf.Grpc.Lite.Internal.Server;

internal interface IServerHandler : IHandler
{
    public void Initialize(ushort id, ChannelWriter<StreamFrame> output, ILogger? logger);
    Status Status { get; set; }
    DateTime Deadline { get; }
    string Host { get; }
    string Peer { get; }
    string Method { get; }
    CancellationToken CancellationToken { get; }
    Metadata RequestHeaders { get; }
    Metadata ResponseTrailers { get; }
    AuthContext AuthContext { get; }
    WriteOptions WriteOptions { get; set; }
    ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options);
    Task WriteResponseHeadersAsyncCore(Metadata responseHeaders);
}
internal abstract class ServerHandler<TRequest, TResponse> : HandlerBase<TRequest>, IServerHandler where TResponse : class where TRequest : class
{
    private ushort _id;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    // these are all set via initialize
    private Method<TRequest, TResponse> _method;
    ChannelWriter<StreamFrame> _output;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private ILogger? _logger;

    protected Method<TRequest, TResponse> Method
    {
        get => _method;
        set => _method = value;
    }
    protected StreamServerCallContext CreateServerCallContext() => StreamServerCallContext.Get(this);

    protected override Marshaller<TRequest> ReceiveMarshaller => _method.RequestMarshaller;
    public void Initialize(ushort id, ChannelWriter<StreamFrame> output, ILogger? logger)
    {
        _output = output;
        _logger = logger;
        _id = id;
        Status = Status.DefaultSuccess;
        Deadline = DateTime.MaxValue;
        Host = Peer = "";
        CancellationToken = default;
        RequestHeaders = Metadata.Empty;
        WriteOptions = WriteOptions.Default;
        _responseTrailers = null;
    }

    string IServerHandler.Method => _method?.FullName ?? "";

    private Metadata? _responseTrailers;
    Metadata IServerHandler.ResponseTrailers => _responseTrailers ??= new Metadata();

    public Status Status { get; set; }
    public DateTime Deadline { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    // therse will all be set in Initialize
    public Metadata RequestHeaders { get; private set; }
    public string Host { get; private set; }
    public string Peer { get; private set; }
    public WriteOptions WriteOptions { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotSupportedException();
    public AuthContext AuthContext => throw new NotSupportedException();

    internal async ValueTask WritePayloadAsync(TResponse response, bool isLastElement)
    {
        var serializationContext = Pool<StreamSerializationContext>.Get();
        try
        {
            _logger.LogDebug(_method, static (state, _) => $"serializing {state.FullName} response...");
            _method.ResponseMarshaller.ContextualSerializer(response, serializationContext);
            _logger.LogDebug(serializationContext, static (state, _) => $"serialized {state.Length} bytes");
            var frames = await serializationContext.WritePayloadAsync(_output, _id, isLastElement, CancellationToken);
            _logger.LogDebug(frames, static (state, _) => $"added {state} payload frames");
        }
        finally
        {
            serializationContext.Recycle();
        }
    }

    internal ValueTask WriteStatusAndTrailers()
    {
        // TODO
        return default;
    }

    public ValueTask WriteHeaders(Metadata responseHeaders)
    {
        return default;
    }

    Task IServerHandler.WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        => WriteHeaders(responseHeaders).AsTask();

}
