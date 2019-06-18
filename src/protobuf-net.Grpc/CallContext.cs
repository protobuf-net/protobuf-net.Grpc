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
        /// <summary>
        /// Default context; all default client options; no server context
        /// </summary>
        public static readonly CallContext Default; // it is **not** accidental that this is a field - allows effective ldsflda usage

        /// <summary>
        /// The options defined on the context; this will be valid for both server and client operations
        /// </summary>
        public CallOptions Client { get; }

        /// <summary>
        /// The server call-context; this will only be valid for server operations
        /// </summary>
        public ServerCallContext? Server { get; }

        /// <summary>
        /// The request headers associated with the operation; this will be valid for both server and client operations
        /// </summary>
        public Metadata RequestHeaders => Client.Headers;

        /// <summary>
        /// The cancellation token associated with the operation; this will be valid for both server and client operations
        /// </summary>
        public CancellationToken CancellationToken => Client.CancellationToken;
        
        /// <summary>
        /// The deadline associated with the operation; this will be valid for both server and client operations
        /// </summary>
        public DateTime? Deadline => Client.Deadline;

        /// <summary>
        /// The write options associated with the operation; this will be valid for both server and client operations
        /// </summary>
        public WriteOptions WriteOptions => Client.WriteOptions;

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        internal MetadataContext? Prepare() => _metadataContext?.Reset();

        /// <summary>
        /// Creates a call-context that represents a server operation
        /// </summary>
        public CallContext(ServerCallContext server)
        {
            Server = server;
            Client = server == null ? default : new CallOptions(server.RequestHeaders, server.Deadline, server.CancellationToken, server.WriteOptions);
            _metadataContext = null;
        }

        /// <summary>
        /// Creates a call-context that represents a client operation
        /// </summary>
        public CallContext(in CallOptions client, CallContextFlags flags = CallContextFlags.None)
        {
            Client = client;
            Server = default;
            _metadataContext = (flags & CallContextFlags.CaptureMetadata) == 0 ? null : new MetadataContext();
        }

        private readonly MetadataContext? _metadataContext;

        /// <summary>
        /// Get the response-headers from a client operation
        /// </summary>
        public Metadata ResponseHeaders() => _metadataContext?.Headers ?? ThrowNoContext<Metadata>();

        /// <summary>
        /// Get the response-trailers from a client operation
        /// </summary>
        public Metadata ResponseTrailers() => _metadataContext?.Trailers ?? ThrowNoContext<Metadata>();

        /// <summary>
        /// Get the response-status from a client operation
        /// </summary>
        public Status ResponseStatus() => _metadataContext?.Status ?? ThrowNoContext<Status>();

        [MethodImpl]
        private T ThrowNoContext<T>()
        {
            if (Server != null) throw new InvalidOperationException("Response metadata is not available for server contexts");
            throw new InvalidOperationException("The CaptureMetadata flag must be specified when creating the CallContext to enable response metadata");
        }
    }

    /// <summary>
    /// Controls the behavior of client-based operations
    /// </summary>
    [Flags]
    public enum CallContextFlags
    {
        /// <summary>
        /// Default options
        /// </summary>
        None = 0,
        /// <summary>
        /// Response metadata (headers, trailers, status) will be captured and made available on the context
        /// </summary>
        CaptureMetadata = 1,
    }
}