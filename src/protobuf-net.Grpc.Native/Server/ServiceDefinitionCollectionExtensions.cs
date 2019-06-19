using Grpc.Core;
using ProtoBuf.Grpc.Internal;
using System;
using System.Linq.Expressions;
using System.Reflection;
using static Grpc.Core.Server;

namespace ProtoBuf.Grpc.Server
{
    /// <summary>
    /// Provides extension methods to the ServiceDefinitionCollection API
    /// </summary>
    public static class ServiceDefinitionCollectionExtensions
    {
        /// <summary>
        /// Adds a code-first service to the available services
        /// </summary>
        public static int AddCodeFirst<TService>(this ServiceDefinitionCollection services, TService service, BinderConfiguration? binderConfiguration = null)
        {
            int count = 0;
            var binder = ServerServiceDefinition.CreateBuilder();
            object?[]? argsBuffer = null;
            Type[] typesBuffer = Array.Empty<Type>();
            string? serviceName;
            if (binderConfiguration == null) binderConfiguration = BinderConfiguration.Default;
            var binderObj = binderConfiguration.Binder;
            foreach (var serviceContract in ContractOperation.ExpandInterfaces(typeof(TService)))
            {
                if (!binderObj.IsServiceContract(serviceContract, out serviceName)) continue;

                foreach (var op in ContractOperation.FindOperations(binderObj, serviceContract))
                {
                    if (ServerInvokerLookup.TryGetValue(op.MethodType, op.Context, op.Result, op.Void, out var invoker)
                        && AddMethod(op.From, op.To, op.Name, op.Method, op.MethodType, invoker))
                    {
                        // added
                        count++;
                    }
                }
            }
            services.Add(binder.Build());
            return count;

            bool AddMethod(Type @in, Type @out, string on, MethodInfo m, MethodType t, Func<MethodInfo, Expression[], Expression>? invoker)
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
                        argsBuffer = new object?[] { serviceName, null, null, null, binder, service, null };
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
                    return false;
                }
            }
        }

        private static readonly MethodInfo s_addMethod = typeof(ServiceDefinitionCollectionExtensions).GetMethod(
           nameof(AddMethod), BindingFlags.Static | BindingFlags.NonPublic)!;

        private static void AddMethod<TService, TRequest, TResponse>(
            string serviceName, string operationName, MethodInfo method, MethodType methodType,
            ServerServiceDefinition.Builder binder, TService service,
            Func<MethodInfo, Expression[], Expression>? invoker)
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            TDelegate As<TDelegate>() where TDelegate : Delegate
            {
                if (invoker == null)
                {
                    // basic - direct call
                    return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), null, method);
                }
                var finalSignature = typeof(TDelegate).GetMethod("Invoke")!;

                var methodParameters = finalSignature.GetParameters();
                Expression[] mapArgs = new Expression[methodParameters.Length + 1];
                var lambdaParameters = Array.ConvertAll(methodParameters, p => Expression.Parameter(p.ParameterType, p.Name));
                mapArgs[0] = Expression.Constant(service, typeof(TService));
                for (int i = 0; i < methodParameters.Length; i++) mapArgs[i + 1] = lambdaParameters[i];
                var body = invoker?.Invoke(method, mapArgs);
                var lambda = Expression.Lambda<TDelegate>(body, lambdaParameters);

                return lambda.Compile();
            }

#pragma warning disable CS8625, CS0618
            var grpcMethod = new Method<TRequest, TResponse>(methodType, serviceName, operationName, DefaultMarshaller<TRequest>.Instance, DefaultMarshaller<TResponse>.Instance);
            switch (methodType)
            {
                case MethodType.Unary:
                    binder.AddMethod(grpcMethod, As<UnaryServerMethod<TRequest, TResponse>>());
                    break;
                case MethodType.ClientStreaming:
                    binder.AddMethod(grpcMethod, As<ClientStreamingServerMethod<TRequest, TResponse>>());
                    break;
                case MethodType.ServerStreaming:
                    binder.AddMethod(grpcMethod, As<ServerStreamingServerMethod<TRequest, TResponse>>());
                    break;
                case MethodType.DuplexStreaming:
                    binder.AddMethod(grpcMethod, As<DuplexStreamingServerMethod<TRequest, TResponse>>());
                    break;
                default:
                    throw new NotSupportedException(methodType.ToString());
            }
#pragma warning restore CS8625, CS0618
        }

    }
}
