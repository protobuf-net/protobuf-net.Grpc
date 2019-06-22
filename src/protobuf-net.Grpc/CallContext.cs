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
        public CallOptions CallOptions
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <summary>
        /// The server call-context; this will only be valid for server operations
        /// </summary>
        public ServerCallContext? ServerCallContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        // for client: could be a MetadataContext, if needed; otherwise: is the "state" object
        // for server: there is never a MetadataContext - and the "state" is always the service instance
        private readonly object? _hybridContext;

        /// <summary>
        /// The request headers associated with the operation; this will be valid for both server and client operations
        /// </summary>
        public Metadata RequestHeaders
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => CallOptions.Headers;
        }

        /// <summary>
        /// The cancellation token associated with the operation; this will be valid for both server and client operations
        /// </summary>
        public CancellationToken CancellationToken
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => CallOptions.CancellationToken;
        }

        /// <summary>
        /// The deadline associated with the operation; this will be valid for both server and client operations
        /// </summary>
        public DateTime? Deadline
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => CallOptions.Deadline;
        }

        /// <summary>
        /// The write options associated with the operation; this will be valid for both server and client operations
        /// </summary>
        public WriteOptions WriteOptions
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => CallOptions.WriteOptions;
        }

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal MetadataContext? Prepare() => MetadataContext?.Reset();

        /// <summary>
        /// Creates a call-context that represents a server operation
        /// </summary>
        public CallContext(object server, ServerCallContext context)
        {
            if (server == null) ThrowNoServerProvided();
            _hybridContext = server;
            ServerCallContext = context;
            CallOptions = context == null ? default : new CallOptions(context.RequestHeaders, context.Deadline, context.CancellationToken, context.WriteOptions);

            static void ThrowNoServerProvided() => throw new ArgumentNullException(nameof(server), "A server instance is required and was not provided");
        }

        /// <summary>
        /// Gets the typed state object that was supplied to this context
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T As<T>() where T : class
        {
            return (State as T) ?? ThrowNoState();
            static T ThrowNoState() => throw new InvalidOperationException("This operation requires a state of type " + typeof(T).Name);
        }

        /// <summary>
        /// Gets the state object that was supplied to this context
        /// </summary>
        public object? State
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _hybridContext is MetadataContext mc ? mc.State : _hybridContext;
        }

        /// <summary>
        /// Creates a call-context that represents a client operation
        /// </summary>
        public CallContext(in CallOptions callOptions = default, CallContextFlags flags = CallContextFlags.None, object? state = null)
        {
            CallOptions = callOptions;
            ServerCallContext = default;
            _hybridContext = (flags & CallContextFlags.CaptureMetadata) == 0 ? state : new MetadataContext(state);
        }

        /// <summary>
        /// Creates a call-context that represents a client operation
        /// </summary>
        public static implicit operator CallContext(in CallOptions options) => new CallContext(in options);

        /// <summary>
        /// Creates a call-context that represents a client operation
        /// </summary>
        public static implicit operator CallContext(CancellationToken cancellationToken) => new CallContext(new CallOptions(cancellationToken: cancellationToken));

        MetadataContext? MetadataContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _hybridContext as MetadataContext;
        }

        /// <summary>
        /// Get the response-headers from a client operation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Metadata ResponseHeaders() => MetadataContext?.Headers ?? ThrowNoContext<Metadata>();

        /// <summary>
        /// Get the response-trailers from a client operation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Metadata ResponseTrailers() => MetadataContext?.Trailers ?? ThrowNoContext<Metadata>();

        /// <summary>
        /// Get the response-status from a client operation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Status ResponseStatus() => MetadataContext?.Status ?? ThrowNoContext<Status>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T ThrowNoContext<T>()
        {
            if (ServerCallContext != null) throw new InvalidOperationException("Response metadata is not available for server contexts");
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