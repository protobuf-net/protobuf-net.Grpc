using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using ProtoBuf.Grpc.Configuration;

namespace ProtoBuf.Grpc.Server
{
    /// <summary>
    /// Provides extension methods to the IServiceCollection API
    /// </summary>
    public static class ServicesExtensions
    {
        /// <summary>
        /// Registers a provider that can recognize and handle code-first services
        /// </summary>
        public static void AddCodeFirstGrpc(this IServiceCollection services)
        {
            services.AddGrpc();
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IServiceMethodProvider<>), typeof(Binder<>)));
        }

        private sealed class Binder<TService> : ServerBinder, IServiceMethodProvider<TService> where TService : class
        {
            private readonly ILogger<Binder<TService>> _logger;
            private readonly BinderConfiguration? _binderConfiguration;
            public Binder(ILoggerFactory loggerFactory, BinderConfiguration? binderConfiguration = null)
            {
                _binderConfiguration = binderConfiguration;
                _logger = loggerFactory.CreateLogger<Binder<TService>>();
            }
            public void OnServiceMethodDiscovery(ServiceMethodProviderContext<TService> context)
            {
                Bind(context, typeof(TService), _binderConfiguration);
            }

            protected override void OnServiceBound(string serviceName, Type serviceContract, int operationCount)
            {
                base.OnServiceBound(serviceName, serviceContract, operationCount);
                if (operationCount != 0) _logger.Log(LogLevel.Information, "{0} implementing service {1} (via '{2}') with {3} operation(s)",
                    typeof(TService), serviceName, serviceContract.Name, operationCount);
            }

#pragma warning disable CS0693 // in reality this will always be the same as the outer TService, so: suppress
            protected override bool OnBind<TService, TRequest, TResponse>(object state, Method<TRequest, TResponse> method, MethodStub stub, TService? service)
#pragma warning restore CS0693
                where TService : class
                where TRequest : class
                where TResponse : class
            {
                var metadata = new List<object>();
                // Add type metadata first so it has a lower priority
                metadata.AddRange(typeof(TService).GetCustomAttributes(inherit: true));
                // Add method metadata last so it has a higher priority
                metadata.AddRange(stub.Method.GetCustomAttributes(inherit: true));
                
                var context = (ServiceMethodProviderContext<TService>)state;
                switch (method.Type)
                {
                    case MethodType.Unary:
                        context.AddUnaryMethod(method, metadata, stub.As<UnaryServerMethod<TService, TRequest, TResponse>>());
                        break;
                    case MethodType.ClientStreaming:
                        context.AddClientStreamingMethod(method, metadata, stub.As<ClientStreamingServerMethod<TService, TRequest, TResponse>>());
                        break;
                    case MethodType.ServerStreaming:
                        context.AddServerStreamingMethod(method, metadata, stub.As<ServerStreamingServerMethod<TService, TRequest, TResponse>>());
                        break;
                    case MethodType.DuplexStreaming:
                        context.AddDuplexStreamingMethod(method, metadata, stub.As<DuplexStreamingServerMethod<TService, TRequest, TResponse>>());
                        break;
                    default:
                        return false;
                }
                return true;
            }
        }
    }
}