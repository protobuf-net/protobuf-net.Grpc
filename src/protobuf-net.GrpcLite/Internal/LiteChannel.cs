using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal.Client;

namespace ProtoBuf.Grpc.Lite.Internal;

public sealed class LiteChannel : ChannelBase, IAsyncDisposable, IDisposable
{
    private readonly IFrameConnection _connection;
    readonly LiteCallInvoker _callInvoker;

    internal LiteChannel(IFrameConnection connection, string target, ILogger? logger = null, CancellationToken cancellationToken = default) : base(target)
    {
        if (!connection.ThreadSafeWrite) connection = new SynchronizedGate(connection, 0);
        _connection = connection;
        _callInvoker = new LiteCallInvoker(connection, logger);
        
        Complete = _callInvoker.ReadAllAsync(logger, cancellationToken);
    }

    internal Task Complete { get; }

    public override CallInvoker CreateCallInvoker() => _callInvoker;

    protected override Task ShutdownAsyncCore() => DisposeAsync().AsTask();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _ = _connection.SafeDisposeAsync().AsTask();
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return _connection.SafeDisposeAsync();
    }
}
