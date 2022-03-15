using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal.Client;

namespace ProtoBuf.Grpc.Lite.Internal;

public sealed class LiteChannel : ChannelBase, IDisposable, IAsyncDisposable
{
    LiteCallInvoker _callInvoker;
    internal LiteChannel(IFrameConnection connection, string target, ILogger? logger = null) : base(target)
    {
        connection = connection.WithThreadSafeWrite();
        _callInvoker = new LiteCallInvoker(target, connection, logger);
        _callInvoker.StartWorker();
    }

    public override CallInvoker CreateCallInvoker() => _callInvoker;

    public void Dispose()
    {
        _callInvoker.StopWorker();
    }

    public ValueTask DisposeAsync()
    {
        _callInvoker.StopWorker();
        return default;
    }

    protected override Task ShutdownAsyncCore()
    {
        _callInvoker.StopWorker();
        return base.ShutdownAsyncCore();
    }

}
