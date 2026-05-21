using Grpc.AspNetCore.Server;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;

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
            services.TryAddSingleton(SimpleRpcExceptionsInterceptor.Instance);
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
                // Wrap context in a typed adapter so ServerBinder can dispatch through the generated path
                // (via IGeneratedServerBindContext) when [GeneratedServer] is present on the contract.
                var adapter = new GeneratedBindAdapter<TService>(context, _binderConfiguration, _logger);
                int count = new Binder(_logger).Bind<TService>(adapter, _binderConfiguration);
                if (count != 0) _logger.Log(LogLevel.Information, "RPC services being provided by {Service}: {Count}", typeof(TService), count);
            }
        }

        // Adapter that both:
        //   - wraps ServiceMethodProviderContext<TService> so the existing reflection path keeps working
        //   - implements IServerMethodBinder<TService> + IGeneratedServerBindContext so the generator's
        //     Bind<TService>(binder) method has somewhere to register against
        private sealed class GeneratedBindAdapter<TService> :
            IGeneratedServerBindContext,
            IServerMethodBinder<TService>
            where TService : class
        {
            private readonly ServiceMethodProviderContext<TService> _context;
            private readonly BinderConfiguration _config;
            private readonly ILogger _logger;
            private Type? _currentContract; // populated during a Bind<TService> call so GetMetadata can resolve

            internal GeneratedBindAdapter(ServiceMethodProviderContext<TService> context, BinderConfiguration? config, ILogger logger)
            {
                _context = context;
                _config = config ?? BinderConfiguration.Default;
                _logger = logger;
            }

            internal ServiceMethodProviderContext<TService> Context => _context;

            BinderConfiguration IServerMethodBinder<TService>.Configuration => _config;

            IList<object> IServerMethodBinder<TService>.GetMetadata(Type contractType, string methodName)
            {
                // Build the same metadata pipeline the reflection path uses for parity
                var method = contractType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                if (method is null) return Array.Empty<object>();
                return _config.Binder.GetMetadata(method, contractType, typeof(TService));
            }

            void IServerMethodBinder<TService>.AddUnaryMethod<TRequest, TResponse>(
                Method<TRequest, TResponse> method, IList<object> metadata,
                UnaryServerHandler<TService, TRequest, TResponse> handler)
            {
                _context.AddUnaryMethod(method, metadata, new UnaryServerMethod<TService, TRequest, TResponse>(handler.Invoke));
                _logger.Log(LogLevel.Debug, "{Service} / {Method} bound from generated server bindings", method.ServiceName, method.Name);
            }

            void IServerMethodBinder<TService>.AddServerStreamingMethod<TRequest, TResponse>(
                Method<TRequest, TResponse> method, IList<object> metadata,
                ServerStreamingServerHandler<TService, TRequest, TResponse> handler)
            {
                _context.AddServerStreamingMethod(method, metadata, new ServerStreamingServerMethod<TService, TRequest, TResponse>(handler.Invoke));
                _logger.Log(LogLevel.Debug, "{Service} / {Method} bound from generated server bindings", method.ServiceName, method.Name);
            }

            void IServerMethodBinder<TService>.AddClientStreamingMethod<TRequest, TResponse>(
                Method<TRequest, TResponse> method, IList<object> metadata,
                ClientStreamingServerHandler<TService, TRequest, TResponse> handler)
            {
                _context.AddClientStreamingMethod(method, metadata, new ClientStreamingServerMethod<TService, TRequest, TResponse>(handler.Invoke));
                _logger.Log(LogLevel.Debug, "{Service} / {Method} bound from generated server bindings", method.ServiceName, method.Name);
            }

            void IServerMethodBinder<TService>.AddDuplexStreamingMethod<TRequest, TResponse>(
                Method<TRequest, TResponse> method, IList<object> metadata,
                DuplexStreamingServerHandler<TService, TRequest, TResponse> handler)
            {
                _context.AddDuplexStreamingMethod(method, metadata, new DuplexStreamingServerMethod<TService, TRequest, TResponse>(handler.Invoke));
                _logger.Log(LogLevel.Debug, "{Service} / {Method} bound from generated server bindings", method.ServiceName, method.Name);
            }

#if NET8_0_OR_GREATER
            [UnconditionalSuppressMessage("AOT", "IL3050",
                Justification = "MakeGenericMethod over typeof(TService); under AOT the closed instantiation Bind<TService> must be statically reachable (typically via the user's service registration).")]
            [UnconditionalSuppressMessage("Trimming", "IL2060",
                Justification = "Same as IL3050: the closed generic instantiation is preserved when the user statically references TService.")]
#endif
            int IGeneratedServerBindContext.InvokeGeneratedBind(
                Type generatedBindingsType, Type contractType, Type serviceType,
                BinderConfiguration binderConfiguration)
            {
                _currentContract = contractType;
                try
                {
                    var bindOpen = generatedBindingsType.GetMethod("Bind",
                        BindingFlags.Public | BindingFlags.Static)
                        ?? throw new InvalidOperationException(
                            $"Generated server bindings class '{generatedBindingsType.FullName}' does not expose a public static Bind<T> method.");
                    var bindClosed = bindOpen.MakeGenericMethod(typeof(TService));
                    var count = (int)bindClosed.Invoke(null, [this])!;
                    return count;
                }
                catch (TargetInvocationException tie) when (tie.InnerException is not null)
                {
                    throw tie.InnerException;
                }
                finally
                {
                    _currentContract = null;
                }
            }
        }

        private sealed class Binder : ServerBinder
        {
            private readonly ILogger _logger;
            internal Binder(ILogger logger)
                => _logger = logger;

            protected internal override void OnWarn(string message, object?[]? args)
                => _logger?.LogWarning(message, args ?? Array.Empty<object>());

            protected internal override void OnError(string message, object?[]? args = null)
                => _logger?.LogError(message, args ?? Array.Empty<object>());

#if NET8_0_OR_GREATER
            [RequiresDynamicCode("Falls through to the Expression.Compile path when generated server bindings are unavailable.")]
            [RequiresUnreferencedCode("Falls through to the reflection-based binding path when generated server bindings are unavailable.")]
#endif
            protected override bool TryBind<TService, TRequest, TResponse>(ServiceBindContext bindContext, Method<TRequest, TResponse> method, MethodStub<TService> stub)
                where TService : class
                where TRequest : class
                where TResponse : class
            {
                // The state is now a GeneratedBindAdapter<TService>; unwrap to find the ServiceMethodProviderContext.
                ServiceMethodProviderContext<TService>? context = bindContext.State switch
                {
                    GeneratedBindAdapter<TService> adapter => adapter.Context,
                    ServiceMethodProviderContext<TService> direct => direct,
                    _ => null,
                };
                if (context is not null)
                {
                    var metadata = bindContext.GetMetadata(stub.Method);
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
                return base.TryBind(bindContext, method, stub);
            }
        }
    }
}
