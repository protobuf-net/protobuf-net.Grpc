using Grpc.Core;
using ProtoBuf.Grpc.Internal;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ProtoBuf.Grpc
{
    /// <summary>
    /// Unifies the API for client and server gRPC call contexts; the API intersection is available
    /// directly - for client-specific or server-specific options: use .Client or .Server; note that
    /// whether this is a client or server context depends on the usage. Silent conversions are available.
    /// </summary>
    public readonly partial struct CallContext
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
        public CallContext(object server, ServerCallContext context)
        {
            if (server == null) ThrowNoServerProvided();
            _server = server;
            Server = context;
            Client = context == null ? default : new CallOptions(context.RequestHeaders, context.Deadline, context.CancellationToken, context.WriteOptions);
            _metadataContext = null;

            static void ThrowNoServerProvided() => throw new ArgumentNullException(nameof(server), "A server instance is required and was not provided");
        }

        internal void AssertServer()
        {
            if (_server == null) ThrowNoServer();
            static void ThrowNoServer() => throw new InvalidOperationException("This operation is only valid on a server context");
        }
        internal T GetServer<T>() where T : class
        {
            return (_server as T) ?? ThrowNoServer();
            static T ThrowNoServer() => throw new InvalidOperationException("This operation requires a server of type " + typeof(T).Name);
        }

        private readonly object? _server;

        /// <summary>
        /// Creates a call-context that represents a client operation
        /// </summary>
        public CallContext(in CallOptions client, CallContextFlags flags = CallContextFlags.None)
        {
            Client = client;
            Server = default;
            _metadataContext = (flags & CallContextFlags.CaptureMetadata) == 0 ? null : new MetadataContext();
            _server = null;
        }

        /// <summary>
        /// Creates a call-context that represents a client operation
        /// </summary>
        public static implicit operator CallContext(in CallOptions options) => new CallContext(in options, CallContextFlags.None);

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