using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Lite.Internal.Server;

internal interface IServerHandler : IStream
{
    Status Status { get; set; }
    DateTime Deadline { get; }
    string Host { get; }
    string Peer { get; }
    CancellationToken CancellationToken { get; }
    Metadata RequestHeaders { get; }
    Metadata ResponseTrailers { get; }
    AuthContext AuthContext { get; }
    WriteOptions WriteOptions { get; set; }
    ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options);
    Task WriteResponseHeadersAsyncCore(Metadata responseHeaders);
    void Initialize(ushort id, IFrameConnection output, ILogger? logger, CancellationToken externalShutdown);
}
internal sealed class ServerStream<TRequest, TResponse> : LiteStream<TResponse, TRequest>, IServerHandler,
        IServerStreamWriter<TResponse>

    where TResponse : class where TRequest : class
{
    protected override ValueTask OnPayloadAsync(TRequest value)
    {
        Logger.ThrowNotImplemented();
        return default;
    }

    public ServerStream(IMethod method, object executor)
        : base(method, null!)
    {
        _executor = executor;
        Status = Status.DefaultSuccess;
        Deadline = DateTime.MaxValue;
        Host = Peer = "";
        RequestHeaders = Metadata.Empty;
        WriteOptions = WriteOptions.Default;
    }
    public void Initialize(ushort id, IFrameConnection output, ILogger? logger, CancellationToken externalShutdown)
    {
        Id = id;
        Logger = logger;
        CancellationToken = externalShutdown; // TODO: register for timeout/external cancellation
        SetOutput(output);
    }

    protected sealed override bool IsClient => false;
    private LiteServerCallContext CreateServerCallContext() => LiteServerCallContext.Get(this);

    protected override Action<TResponse, SerializationContext> Serializer => TypedMethod.ResponseMarshaller.ContextualSerializer;
    protected override Func<DeserializationContext, TRequest> Deserializer => TypedMethod.RequestMarshaller.ContextualDeserializer;

    private Method<TRequest, TResponse> TypedMethod => Unsafe.As<Method<TRequest, TResponse>>(Method);

    private object _executor;

    protected override async ValueTask ExecuteAsync()
    {
        var ctx = CreateServerCallContext();
        try
        {
            switch (_executor)
            {
                case UnaryServerMethod<TRequest, TResponse> unary:
                    Logger.Debug("reading single request...");
                    var request = await AssertNextAsync(ctx.CancellationToken);
                    Logger.Debug((request, method: unary), static (state, _) => $"read: {state.request}; executing {state.method.Method.Name}...");
                    await unary(request, ctx);
                    await AssertNoMoreAsync(ctx.CancellationToken);
                    break;
                case ServerStreamingServerMethod<TRequest, TResponse> serverStreaming:
                    Logger.Debug("reading single request...");
                    request = await AssertNextAsync(ctx.CancellationToken);
                    Logger.Debug((request, method: serverStreaming), static (state, _) => $"read: {state.request}; executing {state.method.Method.Name}...");
                    await serverStreaming(request, this, ctx);
                    await AssertNoMoreAsync(ctx.CancellationToken);
                    break;
                case ClientStreamingServerMethod<TRequest, TResponse> clientStreaming:
                    Logger.Debug(clientStreaming, static (state, _) => $"executing {state.Method.Name}...");
                    await clientStreaming(this, ctx);
                    break;
                case DuplexStreamingServerMethod<TRequest, TResponse> duplexStreaming:
                    Logger.Debug(duplexStreaming, static (state, _) => $"executing {state.Method.Name}...");
                    await duplexStreaming(this, this, ctx);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected executor: {_executor?.GetType()?.Name ?? "(none)"}");
            }
            Logger.Debug("executor completed successfully");
        }
        catch (RpcException rpc)
        {
            Logger.Information(Method, static (state, ex) => $"rpc exception {state.FullName}: {ex!.Message}", rpc);
            var status = rpc.Status;
            if (status.StatusCode == StatusCode.OK)
            {
                // one does not simply fail with success!
                status = new Status(StatusCode.Unknown, status.Detail, status.DebugException);
            }
            Status = status;
        }
        catch (Exception ex)
        {
            Logger.Error(Method, static (state, ex) => $"faulted {state.FullName}: {ex!.Message}", ex);
            Status = new Status(StatusCode.Unknown, "The server encountered an error while performing the operation", ex);
        }
        finally
        {
            ctx.Recycle();
        }
        await WriteStatusAndTrailers();
    }

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

    Task IAsyncStreamWriter<TResponse>.WriteAsync(TResponse message)
        => SendAsync(message, PayloadFlags.None, CancellationToken.None).AsTask();
}
