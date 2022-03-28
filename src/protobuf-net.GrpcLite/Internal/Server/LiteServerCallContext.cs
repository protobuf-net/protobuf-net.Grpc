using Grpc.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite.Internal.Server;

internal sealed class LiteServerCallContext : ServerCallContext, IPooled
{
    private IServerStream? _stream;

    private const bool AllowRecycling = false;

    public static LiteServerCallContext Get(IServerStream stream)
    {

        var obj = AllowRecycling ? Pool<LiteServerCallContext>.Get() : new LiteServerCallContext();
        obj._stream = stream;
        return obj;
    }

    public void Recycle()
    {
        if (AllowRecycling)
        {
#pragma warning disable CS0162 // Unreachable code detected - want to make this turn-off-and-onable
            _stream = null;
            Pool<LiteServerCallContext>.Put(this);
#pragma warning restore CS0162
        }
    }

    protected override string HostCore => _stream!.Host;

    protected override AuthContext AuthContextCore => _stream!.AuthContext;

    protected override CancellationToken CancellationTokenCore => _stream!.CancellationToken;

    protected override DateTime DeadlineCore => _stream!.Deadline;

    protected override string MethodCore => _stream!.Method;

    protected override string PeerCore => _stream!.Peer;

    protected override Metadata RequestHeadersCore => _stream!.RequestHeaders;

    protected override Metadata ResponseTrailersCore => _stream!.ResponseTrailers;

    protected override Status StatusCore
    {
        get => _stream!.Status;
        set => _stream!.Status = value;
    }

    protected override WriteOptions WriteOptionsCore
    {
        get => _stream!.WriteOptions;
        set => _stream!.WriteOptions = value;
    }

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        => _stream!.CreatePropagationTokenCore(options);

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        => _stream!.WriteResponseHeadersAsyncCore(responseHeaders);
}
