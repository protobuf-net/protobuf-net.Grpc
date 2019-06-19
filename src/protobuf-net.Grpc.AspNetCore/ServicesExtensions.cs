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
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IServiceMethodProvider<>), typeof(CodeFirstServiceMethodProvider<>)));
        }

        private sealed class CodeFirstServiceMethodProvider<TService> : IServiceMethodProvider<TService> where TService : class
        {
            private readonly ILogger<CodeFirstServiceMethodProvider<TService>> _logger;
            private readonly BinderConfiguration _binderConfiguration;
            public CodeFirstServiceMethodProvider(ILoggerFactory loggerFactory, BinderConfiguration? binderConfiguration = null)
            {
                _binderConfiguration = binderConfiguration ?? BinderConfiguration.Default;
                _logger = loggerFactory.CreateLogger<CodeFirstServiceMethodProvider<TService>>();
            }
            public void OnServiceMethodDiscovery(ServiceMethodProviderContext<TService> context)
            {
                // ignore any services that are known to be the default handler
                if (Attribute.IsDefined(typeof(TService), typeof(BindServiceMethodAttribute))) return;

                // we support methods that match suitable signatures, where the method is on an
                // interface that TService implements, and the interface is marked [ServiceContract]
                foreach (var iType in ContractOperation.ExpandInterfaces(typeof(TService)))
                {
                    AddMethodsForService(context, iType);
                }
            }

            
            private void AddMethodsForService(ServiceMethodProviderContext<TService> context, Type serviceContract)
            {
                var binder = _binderConfiguration.Binder;
                if (!binder.IsServiceContract(serviceContract, out var serviceName)) return;
                _logger.Log(LogLevel.Trace, "pb-net processing {0}/{1} as {2}", typeof(TService).Name, serviceContract.Name, serviceName);
                object?[]? argsBuffer = null;
                Type[] typesBuffer = Array.Empty<Type>();

                int count = 0;
                foreach (var op in ContractOperation.FindOperations(binder, serviceContract))
                {
                    if (ServerInvokerLookup.TryGetValue(op.MethodType, op.Context, op.Result, op.Void, out var invoker)
                        && AddMethod(op.From, op.To, op.Name, op.Method, op.MethodType, invoker))
                    {
                        // yay!
                        count++;
                    }
                    else
                    {
                        _logger.Log(LogLevel.Warning, "operation cannot be hosted as a server: {0}", op);
                    }
                }
                if (count != 0) _logger.Log(LogLevel.Information, "{0} implementing service {1} (via '{2}') with {3} operation(s)", typeof(TService), serviceName, serviceContract.Name, count);

                bool AddMethod(Type @in, Type @out, string on, MethodInfo m, MethodType t, Func<MethodInfo, ParameterExpression[], Expression>? invoker)
                {
                    try
                    {
                        if (typesBuffer.Length == 0)
                        {
                            typesBuffer = new Type[] { typeof(TService), typeof(void), typeof(void) };
                        }
                        typesBuffer[1] = @in;
                        typesBuffer[2] = @out;

                        if (argsBuffer == null)
                        {
                            argsBuffer = new object?[] { serviceName, null, null, null, context, _logger, null, _binderConfiguration.MarshallerFactory };
                        }
                        argsBuffer[1] = on;
                        argsBuffer[2] = m;
                        argsBuffer[3] = t;
                        argsBuffer[6] = invoker;

                        s_addMethod.MakeGenericMethod(typesBuffer).Invoke(null, argsBuffer);
                        return true;
                    }
                    catch (Exception fail)
                    {
                        if (fail is TargetInvocationException tie) fail = tie.InnerException!;
                        _logger.Log(LogLevel.Error, "Failure processing {0}: {1}", m.Name, fail.Message);
                        return false;
                    }
                }
            }
        }

        private static readonly MethodInfo s_addMethod = typeof(ServicesExtensions).GetMethod(
           nameof(AddMethod), BindingFlags.Static | BindingFlags.NonPublic)!;

        private static void AddMethod<TService, TRequest, TResponse>(
            string serviceName, string operationName, MethodInfo method, MethodType methodType,
            ServiceMethodProviderContext<TService> context, ILogger logger,
            Func<MethodInfo, ParameterExpression[], Expression>? invoker, MarshallerFactory marshallerFactory)
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            var metadata = new List<object>();
            // Add type metadata first so it has a lower priority
            metadata.AddRange(typeof(TService).GetCustomAttributes(inherit: true));
            // Add method metadata last so it has a higher priority
            metadata.AddRange(method.GetCustomAttributes(inherit: true));

            TDelegate As<TDelegate>() where TDelegate : Delegate
            {
                if (invoker == null)
                {
                    // basic - direct call
                    return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), null, method);
                }
                var finalSignature = typeof(TDelegate).GetMethod("Invoke")!;

                var methodParameters = finalSignature.GetParameters();
                var lambdaParameters = Array.ConvertAll(methodParameters, p => Expression.Parameter(p.ParameterType, p.Name));
                var body = invoker?.Invoke(method, lambdaParameters);
                var lambda = Expression.Lambda<TDelegate>(body, lambdaParameters);
                logger.Log(LogLevel.Trace, "mapped {0} via {1}", operationName, lambda);
                return lambda.Compile();
            }

            var grpcMethod = new Method<TRequest, TResponse>(methodType, serviceName, operationName, marshallerFactory.GetMarshaller<TRequest>(), marshallerFactory.GetMarshaller<TResponse>());
            switch (methodType)
            {
                case MethodType.Unary:
                    context.AddUnaryMethod(grpcMethod, metadata, As<UnaryServerMethod<TService, TRequest, TResponse>>());
                    break;
                case MethodType.ClientStreaming:
                    context.AddClientStreamingMethod(grpcMethod, metadata, As<ClientStreamingServerMethod<TService, TRequest, TResponse>>());
                    break;
                case MethodType.ServerStreaming:
                    context.AddServerStreamingMethod(grpcMethod, metadata, As<ServerStreamingServerMethod<TService, TRequest, TResponse>>());
                    break;
                case MethodType.DuplexStreaming:
                    context.AddDuplexStreamingMethod(grpcMethod, metadata, As<DuplexStreamingServerMethod<TService, TRequest, TResponse>>());
                    break;
                default:
                    throw new NotSupportedException(methodType.ToString());
            }
        }

    }
}