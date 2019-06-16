using Grpc.Core;
using ProtoBuf.Grpc.Internal;
using System;
using System.Threading;
using System.Runtime.CompilerServices;
using System.ComponentModel;

namespace ProtoBuf.Grpc
{
    /// <summary>
    /// Unifies the API for client and server gRPC call contexts; the API intersection is available
    /// directly - for client-specific or server-specific options: use .Client or .Server; note that
    /// whether this is a client or server context depends on the usage. Silent conversions are available.
    /// </summary>
    public readonly struct CallContext
    {
        public static readonly CallContext Default; // it is **not** accidental that this is a field - allows effective ldsflda usage

        public CallOptions Client { get; }
        public ServerCallContext? Server { get; }

        public Metadata RequestHeaders => Client.Headers;
        public CancellationToken CancellationToken => Client.CancellationToken;
        public DateTime? Deadline => Client.Deadline;
        public WriteOptions WriteOptions => Client.WriteOptions;

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        internal MetadataContext? Prepare() => _metadataContext?.Reset();

        public CallContext(ServerCallContext server)
        {
            Server = server;
            Client = server == null ? default : new CallOptions(server.RequestHeaders, server.Deadline, server.CancellationToken, server.WriteOptions);
            _metadataContext = null;
        }

        public CallContext(in CallOptions client, CallContextFlags flags = CallContextFlags.None)
        {
            Client = client;
            Server = default;
            _metadataContext = (flags & CallContextFlags.CaptureMetadata) == 0 ? null : new MetadataContext();
        }

        private readonly MetadataContext? _metadataContext;

        public Metadata ResponseHeaders() => _metadataContext?.Headers ?? ThrowNoContext<Metadata>();

        public Metadata ResponseTrailers() => _metadataContext?.Trailers ?? ThrowNoContext<Metadata>();

        public Status ResponseStatus() => _metadataContext?.Status ?? ThrowNoContext<Status>();

        [MethodImpl]
        private T ThrowNoContext<T>()
        {
            if (Server != null) throw new InvalidOperationException("Response metadata is not available for server contexts");
            throw new InvalidOperationException("The CaptureMetadata flag must be specified when creating the CallContext to enable response metadata");
        }
    }

    [Flags]
    public enum CallContextFlags
    {
        None = 0,
        CaptureMetadata = 1,
    }
}