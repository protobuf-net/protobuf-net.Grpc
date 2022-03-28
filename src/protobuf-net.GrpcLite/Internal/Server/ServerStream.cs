using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite.Internal.Server;

internal interface IServerStream : IStream
{
    Status Status { get; set; }
    DateTime Deadline { get; }
    string Host { get; }
    string Peer { get; }
    Metadata RequestHeaders { get; }
    Metadata ResponseTrailers { get; }
    AuthContext AuthContext { get; }
    ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options);
    Task WriteResponseHeadersAsyncCore(Metadata responseHeaders);
    void Initialize(ushort id, IConnection owner, ILogger? logger);
}
internal sealed class ServerStream<TRequest, TResponse> : LiteStream<TResponse, TRequest>, IServerStream,
        IServerStreamWriter<TResponse>

    where TResponse : class where TRequest : class
{
    public ServerStream(IMethod method, object executor)
        : base(method, null!)
    {
        _executor = executor;
        Status = Status.DefaultSuccess;
        Deadline = DateTime.MaxValue;
        Host = Peer = "";
        WriteOptions = WriteOptions.Default;
    }
    public void Initialize(ushort id, IConnection owner, ILogger? logger)
    {
        Id = id;
        SetConnection(owner);
        Logger = logger;
    }

    protected sealed override bool IsClient => false;
    private LiteServerCallContext CreateServerCallContext(out CancellationTokenRegistration cancellationTokenRegistration)
    {
        _externalShutdown = new CancellationTokenSource(); // TODO: only do this if headers suggest it is a possibility
        cancellationTokenRegistration = RegisterForCancellation(_externalShutdown?.Token ?? CancellationToken.None, Deadline); // this is the earliest that we might need an executor CT, and know what we need to do so
        return LiteServerCallContext.Get(this);
    }

    private CancellationTokenSource? _externalShutdown;

    protected override void OnCancel()
    {
        var obj = _externalShutdown;
        if (_externalShutdown is not null)
        {   // make sure we don't hit any cancellation callbacks on the listener thread
            ThreadPool.QueueUserWorkItem(static state =>
            {
                var obj = state as ServerStream<TRequest, TResponse>;
                try
                {
                    obj?._externalShutdown?.Cancel();
                }
                catch (Exception ex)
                {
                    obj?.Logger.Error(ex);
                }
            }, this);
        }
    }

    protected override Action<TResponse, SerializationContext> Serializer => TypedMethod.ResponseMarshaller.ContextualSerializer;
    protected override Func<DeserializationContext, TRequest> Deserializer => TypedMethod.RequestMarshaller.ContextualDeserializer;

    private Method<TRequest, TResponse> TypedMethod => Unsafe.As<Method<TRequest, TResponse>>(Method);

    private object _executor;

    protected override async ValueTask ExecuteAsync()
    {
        Metadata? trailers;
        var ctx = CreateServerCallContext(out var ctr);
        try
        {
            switch (_executor)
            {
                case UnaryServerMethod<TRequest, TResponse> unary:
                    Logger.Debug("reading single request...");
                    var request = await AssertNextAsync();
                    Logger.Debug((request, method: unary), static (state, _) => $"read: {state.request}; executing {state.method.Method.Name}...");
                    var result = await unary(request, ctx);
                    await AssertNoMoreAsync();
                    Logger.Debug(result, static (state, _) => $"sending result {state}...");
                    await SendAsync(result, FrameWriteFlags.BufferHint);
                    break;
                case ServerStreamingServerMethod<TRequest, TResponse> serverStreaming:
                    Logger.Debug("reading single request...");
                    request = await AssertNextAsync();
                    Logger.Debug((request, method: serverStreaming), static (state, _) => $"read: {state.request}; executing {state.method.Method.Name}...");
                    await serverStreaming(request, this, ctx);
                    await AssertNoMoreAsync();
                    break;
                case ClientStreamingServerMethod<TRequest, TResponse> clientStreaming:
                    Logger.Debug(clientStreaming, static (state, _) => $"executing {state.Method.Name}...");
                    result = await clientStreaming(this, ctx);
                    Logger.Debug(result, static (state, _) => $"sending result {state}...");
                    await SendAsync(result, FrameWriteFlags.BufferHint);
                    break;
                case DuplexStreamingServerMethod<TRequest, TResponse> duplexStreaming:
                    Logger.Debug(duplexStreaming, static (state, _) => $"executing {state.Method.Name}...");
                    await duplexStreaming(this, this, ctx);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected executor: {_executor?.GetType()?.Name ?? "(none)"}");
            }
            Logger.Debug("executor completed successfully");
            trailers = ctx.ResponseTrailers;
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
            trailers = rpc.Trailers;
            Status = status;
        }
        catch (Exception ex)
        {
            Logger.Error(Method, static (state, ex) => $"faulted {state.FullName}: {ex!.Message}", ex);
            trailers = null;
            Status = new Status(StatusCode.Unknown, "The server encountered an error while performing the operation", ex);
        }
        finally
        {
            OnComplete();
            ctr.SafeDispose();
            ctx.Recycle();
        }
        await SendTrailerAsync(trailers, Status, FrameWriteFlags.None);
    }

    private Metadata? _responseTrailers;
    Metadata IServerStream.ResponseTrailers => _responseTrailers ??= new Metadata();

    public Status Status { get; set; }
    public DateTime Deadline { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    // therse will all be set in Initialize
    public Metadata RequestHeaders => _requestHeaders ??= MetadataEncoder.GetMetadata(RawHeaders, Connection);

    private Metadata? _requestHeaders;

    public string Host { get; private set; }
    public string Peer { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotSupportedException();
    public AuthContext AuthContext => throw new NotSupportedException();

    private bool _headersSent;
    Task IServerStream.WriteResponseHeadersAsyncCore(Metadata responseHeaders)
    {
        if (_headersSent) ThrowAlreadySent();
        _headersSent = true;
        if (responseHeaders is null || responseHeaders.Count == 0)
        {
            return SendHeaderAsync(null, EmptyCallOptions, FrameWriteFlags.None).AsTask();
        }
        return SendHeaderAsync(null, new CallOptions(headers: responseHeaders), FrameWriteFlags.None).AsTask();

        static void ThrowAlreadySent() => throw new InvalidOperationException("Headers have already been sent");
    }

    public override ValueTask SendAsync(TResponse value, FrameWriteFlags flags)
    {
        if (!_headersSent)
        {
            _headersSent = true;
            var pending = SendHeaderAsync(null, EmptyCallOptions, FrameWriteFlags.BufferHint);
            if (pending.IsCompleted)
            {
                pending.GetAwaiter().GetResult(); // for IVTS+error reasons
            }
            else
            {
                return Awaited(this, pending, value, flags);
            }
        }
        return base.SendAsync(value, flags);

        static async ValueTask Awaited(ServerStream<TRequest, TResponse> server, ValueTask pending, TResponse value, FrameWriteFlags flags)
        {
            await pending;
            await server.SendAsync(value, flags);
        }
    }

    static readonly CallOptions EmptyCallOptions = default(CallOptions);

    Task IAsyncStreamWriter<TResponse>.WriteAsync(TResponse message)
        => SendAsync(message, WriterFlags).AsTask();
}
