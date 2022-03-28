using Grpc.Core;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Lite.Connections;
using ProtoBuf.Grpc.Lite.Internal;
using ProtoBuf.Grpc.Lite.Internal.Client;
using System;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Lite;

/// <summary>
/// A light-weight gRPC <see cref="ChannelBase"/> implementation, for gRPC clients.
/// </summary>
public sealed class LiteChannel : ChannelBase, IDisposable, IAsyncDisposable
{
    LiteCallInvoker _callInvoker;
    internal LiteChannel(IFrameConnection connection, string target, ILogger? logger = null) : base(target)
    {
        _callInvoker = new LiteCallInvoker(target, connection, logger);
        _callInvoker.StartWorker();
    }

    /// <inheritdoc/>
    public override CallInvoker CreateCallInvoker() => _callInvoker;

    /// <summary>
    /// Releases all resources associated with this instance.
    /// </summary>
    public void Dispose()
    {
        _callInvoker.StopWorker();
    }

    /// <summary>
    /// Releases all resources associated with this instance.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _callInvoker.StopWorker();
        return default;
    }

    /// <inheritdoc/>
    protected override Task ShutdownAsyncCore()
    {
        _callInvoker.StopWorker();
        return base.ShutdownAsyncCore();
    }

}
