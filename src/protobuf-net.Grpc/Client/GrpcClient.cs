using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using System;
using System.Reflection;
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
        private readonly BinderConfiguration _binderConfiguration;

        /// <summary>
        /// Create a new client instance for the specified service
        /// </summary>
        public GrpcClient(ChannelBase channel, string serviceName, BinderConfiguration? binderConfiguration = null)
            : this(channel.CreateCallInvoker(), serviceName, binderConfiguration) { }
        /// <summary>
        /// Create a new client instance for the specified service
        /// </summary>
        public GrpcClient(CallInvoker callInvoker, string serviceName, BinderConfiguration? binderConfiguration = null)
        {
            _binderConfiguration = binderConfiguration ?? BinderConfiguration.Default;
            if (string.IsNullOrWhiteSpace(serviceName)) throw new ArgumentException(nameof(serviceName));
            _serviceName = serviceName;
            _callInvoker = callInvoker;
        }
        /// <summary>
        /// Create a new client instance for the specified service, inferring the service name from the type
        /// </summary>
        public GrpcClient(ChannelBase channel, Type contractType, BinderConfiguration? binderConfiguration = null)
            : this(channel.CreateCallInvoker(), GetServiceName(contractType, binderConfiguration), binderConfiguration) { }
        /// <summary>
        /// Create a new client instance for the specified service, inferring the service name from the type
        /// </summary>
        public GrpcClient(CallInvoker callInvoker, Type contractType, BinderConfiguration? binderConfiguration = null)
            : this(callInvoker, GetServiceName(contractType, binderConfiguration), binderConfiguration) { }

        /// <summary>
        /// Invoke the specified gRPC method
        /// </summary>
        public async Task<TResponse> UnaryAsync<TRequest, TResponse>(TRequest request, string methodName,
            CallOptions options = default, string? host = null)
            where TRequest : class
            where TResponse : class
        {
            var marshaller = _binderConfiguration.MarshallerCache;
            var method = new Method<TRequest, TResponse>(MethodType.Unary, _serviceName, methodName,
                marshaller.GetMarshaller<TRequest>(), marshaller.GetMarshaller<TResponse>());
            using var call = _callInvoker.AsyncUnaryCall(method, host, options, request);
            return await call.ResponseAsync.ConfigureAwait(false);
        }

        /// <summary>
        /// Invoke the specified gRPC method
        /// </summary>
        public TResponse BlockingUnary<TRequest, TResponse>(TRequest request, string methodName,
            CallOptions options = default, string? host = null)
            where TRequest : class
            where TResponse : class
        {
            var marshaller = _binderConfiguration.MarshallerCache;
            var method = new Method<TRequest, TResponse>(MethodType.Unary, _serviceName, methodName,
                marshaller.GetMarshaller<TRequest>(), marshaller.GetMarshaller<TResponse>());
            return _callInvoker.BlockingUnaryCall(method, host, options, request);
        }

        /// <summary>
        /// Invoke the specified gRPC method, inferring the operation name from the method
        /// </summary>
        public Task<TResponse> UnaryAsync<TRequest, TResponse>(TRequest request, MethodInfo method,
            CallOptions options = default, string? host = null)
            where TRequest : class
            where TResponse : class
            => UnaryAsync<TRequest, TResponse>(request, GetOperationName(method), options, host);
        /// <summary>
        /// Invoke the specified gRPC method, inferring the operation name from the method
        /// </summary>
        public TResponse BlockingUnary<TRequest, TResponse>(TRequest request, MethodInfo method,
            CallOptions options = default, string? host = null)
            where TRequest : class
            where TResponse : class
            => BlockingUnary<TRequest, TResponse>(request, GetOperationName(method), options, host);

        private static string GetServiceName(Type contractType, BinderConfiguration? binderConfiguration)
        {
            binderConfiguration ??= BinderConfiguration.Default;
            if (!binderConfiguration.Binder.IsServiceContract(contractType, out var name))
                throw new InvalidOperationException("Invalid service type: " + contractType.FullName);
            return name!;
        }
        private string GetOperationName(MethodInfo method)
        {
            if (!_binderConfiguration.Binder.IsOperationContract(method, out var name))
                throw new InvalidOperationException("Invalid operation: " + method.Name);
            return name!;
        }
    }
}
