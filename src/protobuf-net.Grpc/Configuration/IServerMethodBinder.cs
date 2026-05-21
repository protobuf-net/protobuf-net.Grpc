using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Implemented by a <see cref="ServerBinder.Bind"/> caller's <c>state</c> when it wants to
    /// participate in the build-time generated-bindings path. The caller knows <c>TService</c>
    /// statically (e.g. AspNetCore's <c>IServiceMethodProvider&lt;TService&gt;</c>), so it can close
    /// the generic invocation against the generator-emitted <c>Bind&lt;TService&gt;</c> method.
    /// </summary>
    public interface IGeneratedServerBindContext
    {
        /// <summary>
        /// Invoke the generated <c>Bind&lt;TService&gt;</c> method on <paramref name="generatedBindingsType"/>;
        /// returns the number of operations bound.
        /// </summary>
        int InvokeGeneratedBind(
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
#endif
            Type generatedBindingsType,
            Type contractType,
            Type serviceType,
            BinderConfiguration binderConfiguration);
    }

    /// <summary>
    /// Generic unary server handler shape (mirrors Grpc.AspNetCore.Server's UnaryServerMethod&lt;TService, TRequest, TResponse&gt;
    /// but lives in protobuf-net.Grpc so generated code can target it without an AspNetCore dep).
    /// </summary>
    public delegate Task<TResponse> UnaryServerHandler<TService, TRequest, TResponse>(
        TService service, TRequest request, ServerCallContext context)
        where TService : class
        where TRequest : class
        where TResponse : class;

    /// <summary>
    /// Generic server-streaming server handler shape.
    /// </summary>
    public delegate Task ServerStreamingServerHandler<TService, TRequest, TResponse>(
        TService service, TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context)
        where TService : class
        where TRequest : class
        where TResponse : class;

    /// <summary>
    /// Generic client-streaming server handler shape.
    /// </summary>
    public delegate Task<TResponse> ClientStreamingServerHandler<TService, TRequest, TResponse>(
        TService service, IAsyncStreamReader<TRequest> requestStream, ServerCallContext context)
        where TService : class
        where TRequest : class
        where TResponse : class;

    /// <summary>
    /// Generic duplex-streaming server handler shape.
    /// </summary>
    public delegate Task DuplexStreamingServerHandler<TService, TRequest, TResponse>(
        TService service, IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context)
        where TService : class
        where TRequest : class
        where TResponse : class;

    /// <summary>
    /// Receives gRPC method registrations from a build-time-generated server-bindings class.
    /// Generator-emitted <c>Bind&lt;TService&gt;</c> methods call into this so the generated code
    /// stays free of <see cref="System.Linq.Expressions"/> and of any <c>Grpc.AspNetCore.Server</c> dependency.
    /// </summary>
    /// <typeparam name="TService">The concrete service implementation type being bound.</typeparam>
    public interface IServerMethodBinder<TService> where TService : class
    {
        /// <summary>
        /// Register a unary method handler.
        /// </summary>
        void AddUnaryMethod<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            IList<object> metadata,
            UnaryServerHandler<TService, TRequest, TResponse> handler)
            where TRequest : class
            where TResponse : class;

        /// <summary>
        /// Register a server-streaming method handler.
        /// </summary>
        void AddServerStreamingMethod<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            IList<object> metadata,
            ServerStreamingServerHandler<TService, TRequest, TResponse> handler)
            where TRequest : class
            where TResponse : class;

        /// <summary>
        /// Register a client-streaming method handler.
        /// </summary>
        void AddClientStreamingMethod<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            IList<object> metadata,
            ClientStreamingServerHandler<TService, TRequest, TResponse> handler)
            where TRequest : class
            where TResponse : class;

        /// <summary>
        /// Register a duplex-streaming method handler.
        /// </summary>
        void AddDuplexStreamingMethod<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            IList<object> metadata,
            DuplexStreamingServerHandler<TService, TRequest, TResponse> handler)
            where TRequest : class
            where TResponse : class;

        /// <summary>
        /// The configuration to use for resolving marshallers; the generated code reads this
        /// when constructing <see cref="Method{TRequest, TResponse}"/> instances.
        /// </summary>
        BinderConfiguration Configuration { get; }

        /// <summary>
        /// Gather metadata for a generated operation; the generated code passes the contract
        /// interface and method name so the same metadata pipeline (used by the reflection path)
        /// applies here too.
        /// </summary>
        IList<object> GetMetadata(Type contractType, string methodName);
    }
}
