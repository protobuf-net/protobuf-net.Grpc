using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Client
{
    /// <summary>
    /// A general purpose client for calling arbitrary gRPC methods without
    /// any prior contract knowledge or runtime proxy generation
    /// </summary>
    public sealed class GrpcClient
    {
        /// <summary>
        /// Returns the service name of this client
        /// </summary>
        public override string ToString() => _serviceName;

        private readonly CallInvoker _callInvoker;
        private readonly string _serviceName;
        private readonly string? _host;
        private readonly BinderConfiguration _binderConfiguration;

        /// <summary>
        /// Create a new client instance for the specified service
        /// </summary>
        public GrpcClient(ChannelBase channel, string serviceName, BinderConfiguration? binderConfiguration = null, string? host = null)
            : this(channel.CreateCallInvoker(), serviceName, binderConfiguration, host) { }
        /// <summary>
        /// Create a new client instance for the specified service
        /// </summary>
        public GrpcClient(CallInvoker callInvoker, string serviceName, BinderConfiguration? binderConfiguration = null, string? host = null)
        {
            _binderConfiguration = binderConfiguration ?? BinderConfiguration.Default;
            if (string.IsNullOrWhiteSpace(serviceName)) throw new ArgumentException(nameof(serviceName));
            _serviceName = serviceName;
            _callInvoker = callInvoker;
            _host = host;
        }
        /// <summary>
        /// Create a new client instance for the specified service, inferring the service name from the type
        /// </summary>
        public GrpcClient(ChannelBase channel, Type contractType, BinderConfiguration? binderConfiguration = null, string? host = null)
            : this(channel.CreateCallInvoker(), GetServiceName(contractType, binderConfiguration), binderConfiguration, host) { }
        /// <summary>
        /// Create a new client instance for the specified service, inferring the service name from the type
        /// </summary>
        public GrpcClient(CallInvoker callInvoker, Type contractType, BinderConfiguration? binderConfiguration = null, string? host = null)
            : this(callInvoker, GetServiceName(contractType, binderConfiguration), binderConfiguration, host) { }

        /// <summary>
        /// Invoke the specified gRPC method
        /// </summary>
        public Task<TResponse> UnaryAsync<TRequest, TResponse>(TRequest request, string operationName, in CallContext context = default)
            where TRequest : class
            where TResponse : class
        {
            var method = GetMethod<TRequest, TResponse>(MethodType.Unary, operationName);
#pragma warning disable CS0618
            return Reshape.UnaryTaskAsync(context, _callInvoker, method, request, _host);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Invoke the specified gRPC method
        /// </summary>
        public TResponse BlockingUnary<TRequest, TResponse>(TRequest request, string operationName, in CallContext context = default)
            where TRequest : class
            where TResponse : class
        {
            var method = GetMethod<TRequest, TResponse>(MethodType.Unary, operationName);
#pragma warning disable CS0618
            return Reshape.UnarySync(context, _callInvoker, method, request, _host);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Invoke the specified gRPC method
        /// </summary>
        public Task<TResponse> ClientStreamingAsync<TRequest, TResponse>(IAsyncEnumerable<TRequest> request, string operationName, in CallContext context = default)
            where TRequest : class
            where TResponse : class
        {
            var method = GetMethod<TRequest, TResponse>(MethodType.ClientStreaming, operationName);
#pragma warning disable CS0618
            return Reshape.ClientStreamingTaskAsync(context, _callInvoker, method, request, _host);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Invoke the specified gRPC method
        /// </summary>
        public IAsyncEnumerable<TResponse> ServerStreamingAsync<TRequest, TResponse>(TRequest request, string operationName, in CallContext context = default)
            where TRequest : class
            where TResponse : class
        {
            var method = GetMethod<TRequest, TResponse>(MethodType.ServerStreaming, operationName);
#pragma warning disable CS0618
            return Reshape.ServerStreamingAsync(context, _callInvoker, method, request, _host);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Invoke the specified gRPC method
        /// </summary>
        public IAsyncEnumerable<TResponse> DuplexStreamingAsync<TRequest, TResponse>(IAsyncEnumerable<TRequest> request, string operationName, in CallContext context = default)
            where TRequest : class
            where TResponse : class
        {
            var method = GetMethod<TRequest, TResponse>(MethodType.DuplexStreaming, operationName);
#pragma warning disable CS0618
            return Reshape.DuplexAsync(context, _callInvoker, method, request, _host);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Invoke the specified gRPC method, inferring the operation name from the method
        /// </summary>
        public Task<TResponse> UnaryAsync<TRequest, TResponse>(TRequest request, MethodInfo method, in CallContext context = default)
            where TRequest : class
            where TResponse : class
            => UnaryAsync<TRequest, TResponse>(request, GetOperationName(method), context);
        /// <summary>
        /// Invoke the specified gRPC method, inferring the operation name from the method
        /// </summary>
        public TResponse BlockingUnary<TRequest, TResponse>(TRequest request, MethodInfo method, in CallContext context = default)
            where TRequest : class
            where TResponse : class
            => BlockingUnary<TRequest, TResponse>(request, GetOperationName(method), context);
        /// <summary>
        /// Invoke the specified gRPC method, inferring the operation name from the method
        /// </summary>
        public Task<TResponse> ClientStreamingAsync<TRequest, TResponse>(IAsyncEnumerable<TRequest> request, MethodInfo method, in CallContext context = default)
            where TRequest : class
            where TResponse : class
            => ClientStreamingAsync<TRequest, TResponse>(request, GetOperationName(method), context);
        /// <summary>
        /// Invoke the specified gRPC method, inferring the operation name from the method
        /// </summary>
        public IAsyncEnumerable<TResponse> ServerStreamingAsync<TRequest, TResponse>(TRequest request, MethodInfo method, in CallContext context = default)
            where TRequest : class
            where TResponse : class
            => ServerStreamingAsync<TRequest, TResponse>(request, GetOperationName(method), context);
        /// <summary>
        /// Invoke the specified gRPC method, inferring the operation name from the method
        /// </summary>
        public IAsyncEnumerable<TResponse> DuplexStreamingAsync<TRequest, TResponse>(IAsyncEnumerable<TRequest> request, MethodInfo method, in CallContext context = default)
            where TRequest : class
            where TResponse : class
            => DuplexStreamingAsync<TRequest, TResponse>(request, GetOperationName(method), context);


        private static string GetServiceName(Type contractType, BinderConfiguration? binderConfiguration)
        {
            binderConfiguration ??= BinderConfiguration.Default;
            if (!binderConfiguration.Binder.IsServiceContract(contractType, out var name))
                throw new InvalidOperationException("Invalid service type: " + contractType.FullName);
            return name!;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Method<TRequest, TResponse> GetMethod<TRequest, TResponse>(MethodType methodType, string name)
        {
            var marshallerCache = _binderConfiguration.MarshallerCache;
            return new Method<TRequest, TResponse>(methodType, _serviceName, name,
                marshallerCache.GetMarshaller<TRequest>(), marshallerCache.GetMarshaller<TResponse>());
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetOperationName(MethodInfo method)
        {
            if (!_binderConfiguration.Binder.IsOperationContract(method, out var name))
                ThrowInvalid(method);
            return name!;
            static void ThrowInvalid(MethodInfo method) =>
                throw new InvalidOperationException("Invalid operation: " + method.Name);
        }
    }
}
