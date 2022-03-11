using Grpc.Core;

namespace ProtoBuf.Grpc.Lite.Internal.Server;

internal sealed class StreamServerCallContext : ServerCallContext, IPooled
{
    private IServerHandler? _handler;

    private const bool AllowRecycling = false;

    public static StreamServerCallContext Get(IServerHandler handler)
    {
        var obj = AllowRecycling ? Pool<StreamServerCallContext>.Get() : new StreamServerCallContext();
        obj._handler = handler;
        return obj;
    }

    public void Recycle()
    {
        if (AllowRecycling)
        {
#pragma warning disable CS0162 // Unreachable code detected - want to make this turn-off-and-onable
            _handler = null;
            Pool<StreamServerCallContext>.Put(this);
#pragma warning restore CS0162
        }
    }

    protected override string HostCore => _handler!.Host;

    protected override AuthContext AuthContextCore => _handler!.AuthContext;

    protected override CancellationToken CancellationTokenCore => _handler!.CancellationToken;

    protected override DateTime DeadlineCore => _handler!.Deadline;

    protected override string MethodCore => _handler!.Method;

    protected override string PeerCore => _handler!.Peer;

    protected override Metadata RequestHeadersCore => _handler!.RequestHeaders;

    protected override Metadata ResponseTrailersCore => _handler!.ResponseTrailers;

    protected override Status StatusCore
    {
        get => _handler!.Status;
        set => _handler!.Status = value;
    }

    protected override WriteOptions WriteOptionsCore
    {
        get => _handler!.WriteOptions;
        set => _handler!.WriteOptions = value;
    }

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        => _handler!.CreatePropagationTokenCore(options);

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        => _handler!.WriteResponseHeadersAsyncCore(responseHeaders);
}
