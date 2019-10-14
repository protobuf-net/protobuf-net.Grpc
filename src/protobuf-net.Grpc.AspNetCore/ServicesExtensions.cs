using Grpc.AspNetCore.Server;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Configuration;
using System;
using System.Collections.Generic;

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
        public static void AddCodeFirstGrpc(this IServiceCollection services) => AddCodeFirstGrpc(services, null);

        /// <summary>
        /// Registers a provider that can recognize and handle code-first services
        /// </summary>
        public static IGrpcServerBuilder AddCodeFirstGrpc(this IServiceCollection services, Action<GrpcServiceOptions>? configureOptions)
        {
            var builder = configureOptions == null ? services.AddGrpc() : services.AddGrpc(configureOptions);
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IServiceMethodProvider<>), typeof(CodeFirstServiceMethodProvider<>)));
            return builder;
        }

        private sealed class CodeFirstServiceMethodProvider<TService> : IServiceMethodProvider<TService> where TService : class
        {
            private readonly ILogger<CodeFirstServiceMethodProvider<TService>> _logger;
            private readonly BinderConfiguration? _binderConfiguration;
            public CodeFirstServiceMethodProvider(ILoggerFactory loggerFactory, BinderConfiguration? binderConfiguration = null)
            {
                _binderConfiguration = binderConfiguration;
                _logger = loggerFactory.CreateLogger<CodeFirstServiceMethodProvider<TService>>();
            }

            void IServiceMethodProvider<TService>.OnServiceMethodDiscovery(ServiceMethodProviderContext<TService> context)
            {
                int count = Binder.Instance.Bind<TService>(context, _binderConfiguration);
                if (count != 0) _logger.Log(LogLevel.Information, "RPC services being provided by {0}: {1}", typeof(TService), count);
            }
        }
        private sealed class Binder : ServerBinder
        {
            private Binder() { }
            public static readonly Binder Instance = new Binder();

            protected override bool TryBind<TService, TRequest, TResponse>(object state, Method<TRequest, TResponse> method, MethodStub<TService> stub)
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
                        context.AddUnaryMethod(method, metadata, stub.CreateDelegate<UnaryServerMethod<TService, TRequest, TResponse>>());
                        break;
                    case MethodType.ClientStreaming:
                        context.AddClientStreamingMethod(method, metadata, stub.CreateDelegate<ClientStreamingServerMethod<TService, TRequest, TResponse>>());
                        break;
                    case MethodType.ServerStreaming:
                        context.AddServerStreamingMethod(method, metadata, stub.CreateDelegate<ServerStreamingServerMethod<TService, TRequest, TResponse>>());
                        break;
                    case MethodType.DuplexStreaming:
                        context.AddDuplexStreamingMethod(method, metadata, stub.CreateDelegate<DuplexStreamingServerMethod<TService, TRequest, TResponse>>());
                        break;
                    default:
                        return false;
                }
                return true;
            }
        }
    }
}