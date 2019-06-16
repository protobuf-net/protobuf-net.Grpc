using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Internal;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.ServiceModel;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Server
{
    public static class ServicesExtensions
    {
        public static void AddCodeFirstGrpc(this IServiceCollection services)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IServiceMethodProvider<>), typeof(CodeFirstServiceMethodProvider<>)));
        }

        private sealed class CodeFirstServiceMethodProvider<TService> : IServiceMethodProvider<TService> where TService : class
        {
            private readonly ILogger<CodeFirstServiceMethodProvider<TService>> _logger;

            public CodeFirstServiceMethodProvider(ILoggerFactory loggerFactory)
            {
                _logger = _logger = loggerFactory.CreateLogger<CodeFirstServiceMethodProvider<TService>>();
            }
            public void OnServiceMethodDiscovery(ServiceMethodProviderContext<TService> context)
            {
                // ignore any services that are known to be the default handler
                if (Attribute.IsDefined(typeof(TService), typeof(BindServiceMethodAttribute))) return;

                // we support methods that match suitable signatures, where:
                // 1. (removed - no longer supported) the method is directly on TService and is marked [OperationContract]
                // 2. the method is on an interface that TService implements, and the interface is marked [ServiceContract]
                // AddMethodsForService(context,typeof(TService));

                foreach (var iType in typeof(TService).GetInterfaces())
                {
                    AddMethodsForService(context, iType);
                }
            }

            static Expression ToTaskT(Expression expression)
            {
                var type = expression.Type;
                if (type.IsGenericType)
                {
                    if (type.GetGenericTypeDefinition() == typeof(Task<>))
                        return expression;
                    if (type.GetGenericTypeDefinition() == typeof(ValueTask<>))
                        return Expression.Call(expression, nameof(ValueTask<int>.AsTask), null);
                }
                return Expression.Call(typeof(Task), nameof(Task.FromResult), new Type[] { expression.Type }, expression);
            }

            internal static readonly ConstructorInfo s_CallContext_FromServerContext = typeof(CallContext).GetConstructor(new[] { typeof(ServerCallContext) })!;
            static Expression ToCallContext(Expression context) => Expression.New(s_CallContext_FromServerContext, context);
#pragma warning disable CS0618
            static Expression AsAsyncEnumerable(ParameterExpression value, ParameterExpression context)
                => Expression.Call(typeof(Reshape), nameof(Reshape.AsAsyncEnumerable),
                    typeArguments: value.Type.GetGenericArguments(),
                    arguments: new Expression[] { value, Expression.Property(context, nameof(ServerCallContext.CancellationToken)) });

            static Expression WriteTo(Expression value, ParameterExpression writer, ParameterExpression context)
                => Expression.Call(typeof(Reshape), nameof(Reshape.WriteTo),
                    typeArguments: value.Type.GetGenericArguments(),
                    arguments: new Expression[] {value, writer, Expression.Property(context, nameof(ServerCallContext.CancellationToken)) });
#pragma warning restore CS0618

            static readonly Dictionary<(MethodType, ContextKind, ResultKind), Func<MethodInfo, ParameterExpression[], Expression>?> _invokers
                = new Dictionary<(MethodType, ContextKind, ResultKind), Func<MethodInfo, ParameterExpression[], Expression>?>
            {
                // GRPC-style server methods are direct match; no mapping required
                // => service.{method}(args)
                { (MethodType.Unary, ContextKind.ServerCallContext, ResultKind.Task), null },
                { (MethodType.ServerStreaming, ContextKind.ServerCallContext, ResultKind.Task), null },
                { (MethodType.ClientStreaming, ContextKind.ServerCallContext, ResultKind.Task), null },
                { (MethodType.DuplexStreaming, ContextKind.ServerCallContext, ResultKind.Task), null },

                // Unary: Task<TResponse> Foo(TService service, TRequest request, ServerCallContext serverCallContext);
                // => service.{method}(request, [new CallContext(serverCallContext)])
                {(MethodType.Unary, ContextKind.NoContext, ResultKind.Task), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1])) },
                {(MethodType.Unary, ContextKind.NoContext, ResultKind.ValueTask), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1])) },
                {(MethodType.Unary, ContextKind.NoContext, ResultKind.Sync), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1])) },

                {(MethodType.Unary, ContextKind.CallContext, ResultKind.Task), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1], ToCallContext(args[2]))) },
                {(MethodType.Unary, ContextKind.CallContext, ResultKind.ValueTask), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1], ToCallContext(args[2]))) },
                {(MethodType.Unary, ContextKind.CallContext, ResultKind.Sync), (method, args) => ToTaskT(Expression.Call(args[0], method, args[1], ToCallContext(args[2]))) },

                // Client Streaming: Task<TResponse> Foo(TService service, IAsyncStreamReader<TRequest> stream, ServerCallContext serverCallContext);
                // => service.{method}(reader.AsAsyncEnumerable(serverCallContext.CancellationToken), [new CallContext(serverCallContext)])
                {(MethodType.ClientStreaming, ContextKind.NoContext, ResultKind.Task), (method, args) => ToTaskT(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[2]))) },
                {(MethodType.ClientStreaming, ContextKind.NoContext, ResultKind.ValueTask), (method, args) => ToTaskT(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[2]))) },
                {(MethodType.ClientStreaming, ContextKind.NoContext, ResultKind.Sync), (method, args) => ToTaskT(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[2]))) },

                {(MethodType.ClientStreaming, ContextKind.CallContext, ResultKind.Task), (method, args) => ToTaskT(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[2]), ToCallContext(args[2]))) },
                {(MethodType.ClientStreaming, ContextKind.CallContext, ResultKind.ValueTask), (method, args) => ToTaskT(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[2]), ToCallContext(args[2]))) },
                {(MethodType.ClientStreaming, ContextKind.CallContext, ResultKind.Sync), (method, args) => ToTaskT(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[2]), ToCallContext(args[2]))) },

                // Server Streaming: Task Foo(TService service, TRequest request, IServerStreamWriter<TResponse> stream, ServerCallContext serverCallContext);
                // => service.{method}(request, [new CallContext(serverCallContext)]).WriteTo(stream, serverCallContext.CancellationToken)
                {(MethodType.ServerStreaming, ContextKind.NoContext, ResultKind.AsyncEnumerable), (method, args) => WriteTo(Expression.Call(args[0], method, args[1]), args[2], args[3])},
                {(MethodType.ServerStreaming, ContextKind.CallContext, ResultKind.AsyncEnumerable), (method, args) => WriteTo(Expression.Call(args[0], method, args[1], ToCallContext(args[3])), args[2], args[3])},

                // Duplex: Task Foo(TService service, IAsyncStreamReader<TRequest> input, IServerStreamWriter<TResponse> output, ServerCallContext serverCallContext);
                // => service.{method}(input.AsAsyncEnumerable(serverCallContext.CancellationToken), [new CallContext(serverCallContext)]).WriteTo(output, serverCallContext.CancellationToken)
                {(MethodType.DuplexStreaming, ContextKind.NoContext, ResultKind.AsyncEnumerable), (method, args) => WriteTo(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[3])), args[2], args[3]) },
                {(MethodType.DuplexStreaming, ContextKind.CallContext, ResultKind.AsyncEnumerable), (method, args) => WriteTo(Expression.Call(args[0], method, AsAsyncEnumerable(args[1], args[3]), ToCallContext(args[3])), args[2], args[3]) },
            };
            private void AddMethodsForService(ServiceMethodProviderContext<TService> context, Type serviceContract)
            {
                bool isPublicContract = typeof(TService) == serviceContract;
                if (!ContractOperation.TryGetServiceName(serviceContract, out var serviceName, !isPublicContract)) return;
                _logger.Log(LogLevel.Trace, "pb-net processing {0}/{1} as {2}", typeof(TService).Name, serviceContract.Name, serviceName);
                object?[]? argsBuffer = null;
                Type[] typesBuffer = Array.Empty<Type>();

                int count = 0;
                foreach (var op in ContractOperation.FindOperations(serviceContract, isPublicContract))
                {
                    if (_invokers.TryGetValue((op.MethodType, op.Context, op.Result), out var invoker)
                        && AddMethod(op.From, op.To, op.Method, op.MethodType, invoker))
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

                bool AddMethod(Type @in, Type @out, MethodInfo m, MethodType t, Func<MethodInfo, ParameterExpression[], Expression>? invoker = null)
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
                            argsBuffer = new object?[] { serviceName, null, null, context, _logger, null };
                        }
                        argsBuffer[1] = m;
                        argsBuffer[2] = t;
                        argsBuffer[5] = invoker;

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
            string serviceName, MethodInfo method, MethodType methodType,
            ServiceMethodProviderContext<TService> context, ILogger logger,
            Func<MethodInfo, ParameterExpression[], Expression>? invoker = null)
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            var oca = (OperationContractAttribute?)Attribute.GetCustomAttribute(method, typeof(OperationContractAttribute), inherit: true);
            var operationName = oca?.Name;
            if (string.IsNullOrWhiteSpace(operationName))
            {
                operationName = method.Name;
                if (operationName.EndsWith("Async")) operationName = operationName.Substring(0, operationName.Length - 5);
            }

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

#pragma warning disable CS8625
            switch (methodType)
            {
                case MethodType.Unary:
                    context.AddUnaryMethod(
                        new FullyNamedMethod<TRequest, TResponse>(operationName, methodType, serviceName, method.Name), metadata, As<UnaryServerMethod<TService, TRequest, TResponse>>());
                    break;
                case MethodType.ClientStreaming:
                    context.AddClientStreamingMethod(
                        new FullyNamedMethod<TRequest, TResponse>(operationName, methodType, serviceName, method.Name), metadata, As<ClientStreamingServerMethod<TService, TRequest, TResponse>>());
                    break;
                case MethodType.ServerStreaming:
                    context.AddServerStreamingMethod(
                        new FullyNamedMethod<TRequest, TResponse>(operationName, methodType, serviceName, method.Name), metadata, As<ServerStreamingServerMethod<TService, TRequest, TResponse>>());
                    break;
                case MethodType.DuplexStreaming:
                    context.AddDuplexStreamingMethod(
                        new FullyNamedMethod<TRequest, TResponse>(operationName, methodType, serviceName, method.Name), metadata, As<DuplexStreamingServerMethod<TService, TRequest, TResponse>>());
                    break;
                default:
                    throw new NotSupportedException(methodType.ToString());
            }
#pragma warning restore CS8625
        }
    }
}