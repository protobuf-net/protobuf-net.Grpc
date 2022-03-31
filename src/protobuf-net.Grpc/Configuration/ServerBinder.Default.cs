using Grpc.Core;
using System;
using System.IO;

namespace ProtoBuf.Grpc.Configuration
{
    partial class ServerBinder
    {
        private static ServerBinder? s_Default;
        internal static ServerBinder Default => s_Default ??= new ServerBinder(null);
        TextWriter? _log;

        /// <summary>
        /// Create a new binder instance.
        /// </summary>
        protected ServerBinder() : this(null) { }
        internal ServerBinder(TextWriter? log)
        {
            _log = log;
        }

        /// <summary>
        /// The implementing binder should bind the method to the bind-state
        /// </summary>
        protected virtual bool TryBind<TService, TRequest, TResponse>(ServiceBindContext bindContext, Method<TRequest, TResponse> method, MethodStub<TService> stub)
                where TService : class
                where TRequest : class
                where TResponse : class
        {
            try
            {
                if (bindContext.State is ServiceBinderBase binder)
                {
                    switch (method.Type)
                    {
                        case MethodType.Unary:
                            binder.AddMethod(method, stub.CreateDelegate<UnaryServerMethod<TRequest, TResponse>>());
                            break;
                        case MethodType.ClientStreaming:
                            binder.AddMethod(method, stub.CreateDelegate<ClientStreamingServerMethod<TRequest, TResponse>>());
                            break;
                        case MethodType.ServerStreaming:
                            binder.AddMethod(method, stub.CreateDelegate<ServerStreamingServerMethod<TRequest, TResponse>>());
                            break;
                        case MethodType.DuplexStreaming:
                            binder.AddMethod(method, stub.CreateDelegate<DuplexStreamingServerMethod<TRequest, TResponse>>());
                            break;
                        default:
                            return false;
                    }
                }
                else if (bindContext.State is ServerServiceDefinition.Builder builder)
                {
                    switch (method.Type)
                    {
                        case MethodType.Unary:
                            builder.AddMethod(method, stub.CreateDelegate<UnaryServerMethod<TRequest, TResponse>>());
                            break;
                        case MethodType.ClientStreaming:
                            builder.AddMethod(method, stub.CreateDelegate<ClientStreamingServerMethod<TRequest, TResponse>>());
                            break;
                        case MethodType.ServerStreaming:
                            builder.AddMethod(method, stub.CreateDelegate<ServerStreamingServerMethod<TRequest, TResponse>>());
                            break;
                        case MethodType.DuplexStreaming:
                            builder.AddMethod(method, stub.CreateDelegate<DuplexStreamingServerMethod<TRequest, TResponse>>());
                            break;
                        default:
                            return false;
                    }
                }
                else
                {
                    return false; // unexpected state object
                }
                return true;
            }
            catch (Exception ex)
            {
                OnError(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Publish a warning message
        /// </summary>
        protected internal virtual void OnWarn(string message, object?[]? args = null)
            => _log?.WriteLine("[warning] " + message, args ?? Array.Empty<object>());

        /// <summary>
        /// Publish an error message
        /// </summary>
        protected internal virtual void OnError(string message, object?[]? args = null)
            => _log?.WriteLine("[error] " + message, args ?? Array.Empty<object>());

        /// <summary>
        /// Reports the number of operations available for a service
        /// </summary>
        protected virtual void OnServiceBound(object state, string serviceName, Type serviceType, Type serviceContract, int operationCount)
            => _log?.WriteLine($"{serviceName} bound to {serviceType.Name} : {serviceContract.Name} with {operationCount} operation(s)");

        /// <summary>
        /// Create a new binder instance
        /// </summary>
        public static ServerBinder Create(TextWriter? log) => log == null ? Default : new ServerBinder(log);
    }
}
