using Grpc.Core;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Helper API for binding servers; this is an advanced API that would only be used if you are implementing a new transport provider
    /// </summary>
    public abstract class ServerBinder : IBindContext
    {
        /*
         warning: this code is reflection hell - lots of switching from Type to <T>; it isn't pretty,
         but the API it *provides* is pretty clean; better to hide this mess in the lib than have
         implementations have to worry about it. This also ensures that the binderConfiguration rules
         are observed and respected
        */

        /// <summary>
        /// Initiate a bind operation, causing all service methods to be crawled for the provided type
        /// </summary>
        public int Bind<TService>(object state, BinderConfiguration? binderConfiguration = null, TService? service = null)
            where TService : class
            => Bind(state, typeof(TService), binderConfiguration, service);

        /// <summary>
        /// Initiate a bind operation, causing all service methods to be crawled for the provided type
        /// </summary>
        public int Bind(object state, Type serviceType, BinderConfiguration? binderConfiguration = null, object? service = null)
        {
            int totalCount = 0;
            object?[]? argsBuffer = null;
            Type[] typesBuffer = Array.Empty<Type>();
            string? serviceName;
            if (binderConfiguration == null) binderConfiguration = BinderConfiguration.Default;
            var serviceContracts = typeof(IGrpcService).IsAssignableFrom(serviceType)
                ? new HashSet<Type> { serviceType }
                : ContractOperation.ExpandInterfaces(serviceType);

            bool serviceImplSimplifiedExceptions = serviceType.IsDefined(typeof(SimpleRpcExceptionsAttribute));
            foreach (var serviceContract in serviceContracts)
            {
                if (!binderConfiguration.Binder.IsServiceContract(serviceContract, out serviceName)) continue;

                var serviceContractSimplifiedExceptions = serviceImplSimplifiedExceptions || serviceContract.IsDefined(typeof(SimpleRpcExceptionsAttribute));
                int svcOpCount = 0;
                var bindCtx = new ServiceBindContext(serviceContract, serviceType, state);
                foreach (var op in ContractOperation.FindOperations(binderConfiguration, serviceContract, this))
                {
                    if (ServerInvokerLookup.TryGetValue(op.MethodType, op.Context, op.Result, op.Void, out var invoker)
                        && AddMethod(op.From, op.To, op.Name, op.Method, op.MethodType, invoker, bindCtx,
                        serviceContractSimplifiedExceptions || op.Method.IsDefined(typeof(SimpleRpcExceptionsAttribute))
                        ))
                    {
                        // yay!
                        totalCount++;
                        svcOpCount++;
                    }
                }
                OnServiceBound(state, serviceName!, serviceType, serviceContract, svcOpCount);
            }
            return totalCount;

            bool AddMethod(Type @in, Type @out, string on, MethodInfo m, MethodType t,
                Func<MethodInfo, ParameterExpression[], Expression>? invoker, ServiceBindContext bindContext, bool simplifiedExceptionHandling)
            {
                try
                {
                    if (typesBuffer.Length == 0)
                    {
                        typesBuffer = new Type[] { serviceType, typeof(void), typeof(void) };
                    }
                    typesBuffer[1] = @in;
                    typesBuffer[2] = @out;

                    if (argsBuffer is null)
                    {
                        argsBuffer = new object?[9];
                        argsBuffer[6] = binderConfiguration!.MarshallerCache;
                        argsBuffer[7] = service is null ? null : Expression.Constant(service, serviceType);
                    }
                    argsBuffer[0] = serviceName;
                    argsBuffer[1] = on;
                    argsBuffer[2] = m;
                    argsBuffer[3] = t;
                    argsBuffer[4] = bindContext;
                    argsBuffer[5] = invoker;
                    // 6, 7 set during array initialization
                    argsBuffer[8] = simplifiedExceptionHandling;

                    return (bool)s_addMethod.MakeGenericMethod(typesBuffer).Invoke(this, argsBuffer)!;
                }
                catch (Exception fail)
                {
                    if (fail is TargetInvocationException tie) fail = tie.InnerException!;
                    return false;
                }
            }
        }

        /// <summary>
        /// Reports the number of operations available for a service
        /// </summary>
        protected virtual void OnServiceBound(object state, string serviceName, Type serviceType, Type serviceContract, int operationCount) { }

        private static readonly MethodInfo s_addMethod = typeof(ServerBinder).GetMethod(
            nameof(AddMethod), BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static class ParameterCache<TDelegate> where TDelegate : Delegate
        {
            internal static readonly ParameterExpression[] Parameters
                = Array.ConvertAll(typeof(TDelegate).GetMethod(nameof(Action.Invoke))!.GetParameters(),
                    p => Expression.Parameter(p.ParameterType, p.Name));
        }

        /// <summary>
        /// Provides utilities associated with the method being considered
        /// </summary>
        protected readonly struct MethodStub<TService>
            where TService : class
        {
            private readonly ConstantExpression? _service;
            private readonly Func<MethodInfo, Expression[], Expression>? _invoker;
            private readonly bool _simpleExceptionHandling;

            /// <summary>
            /// The runtime method being considered
            /// </summary>
            public MethodInfo Method { get; }

            internal MethodStub(Func<MethodInfo, Expression[], Expression>? invoker, MethodInfo method, ConstantExpression? service, bool simpleExceptionHandling)
            {
                _simpleExceptionHandling = simpleExceptionHandling;
                _invoker = invoker;
                _service = service;
                Method = method;
            }

            /// <summary>
            /// Create a delegate that will invoke this method against a constant instance of the service
            /// </summary>
            public TDelegate CreateDelegate<TDelegate>()
                where TDelegate : Delegate
            {
                if (_invoker == null)
                {
                    if (_simpleExceptionHandling)
                    {
                        var lambdaArgs = ParameterCache<TDelegate>.Parameters;

                        var call = _service is null
                            ? Expression.Call(Method, lambdaArgs)
                            : Expression.Call(_service, Method, lambdaArgs);

                        return Expression.Lambda<TDelegate>(
                            ApplySimpleExceptionHandling(call), lambdaArgs).Compile();
                    }
                    else
                    {
                        // basic - direct call
                        return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), _service, Method);
                    }
                }
                else
                {
                    var lambdaArgs = ParameterCache<TDelegate>.Parameters;

                    Expression[] mapArgs;
                    if (_service is null)
                    {   // if no service object, then the service is part of the signature, i.e. (svc, req) => svc.Blah();
                        mapArgs = lambdaArgs;
                    }
                    else
                    {
                        // if there *is* a service object, then that is *not* part of the signature, i.e. (req) => svc.Blah(req)
                        // where the svc instance comes in separately
                        mapArgs = new Expression[lambdaArgs.Length + 1];
                        mapArgs[0] = _service;
                        lambdaArgs.CopyTo(mapArgs, 1);
                    }

                    var body = _invoker.Invoke(Method, mapArgs);
                    if (_simpleExceptionHandling)
                    {
                        body = ApplySimpleExceptionHandling(body);
                    }
                    var lambda = Expression.Lambda<TDelegate>(body, lambdaArgs);

                    return lambda.Compile();
                }
            }

            static Expression ApplySimpleExceptionHandling(Expression body)
            {
                var type = body.Type;
                if (type == typeof(Task))
                {
                    body = Expression.Call(s_ReshapeWithSimpleExceptionHandling[0], body);
                }
                else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    body = Expression.Call(s_ReshapeWithSimpleExceptionHandling[1].MakeGenericMethod(type.GetGenericArguments()), body);
                }
                return body;
            }
        }

#pragma warning disable CS0618
        private static readonly Dictionary<int, MethodInfo> s_ReshapeWithSimpleExceptionHandling =
            (from method in typeof(Reshape).GetMethods(BindingFlags.Public | BindingFlags.Static)
             where method.Name is nameof(Reshape.WithSimpleExceptionHandling)
             select method)
            .ToDictionary(method => method.IsGenericMethodDefinition ? method.GetGenericArguments().Length : 0);
#pragma warning restore CS0618

        private bool AddMethod<TService, TRequest, TResponse>(
            string serviceName, string operationName, MethodInfo method, MethodType methodType,
            ServiceBindContext bindContext,
            Func<MethodInfo, Expression[], Expression>? invoker, MarshallerCache marshallerCache,
            ConstantExpression? service, bool simplfiedExceptionHandling)
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            var grpcMethod = new Method<TRequest, TResponse>(methodType, serviceName, operationName, marshallerCache.GetMarshaller<TRequest>(), marshallerCache.GetMarshaller<TResponse>());
            var stub = new MethodStub<TService>(invoker, method, service, simplfiedExceptionHandling);
            try
            {
                return TryBind<TService, TRequest, TResponse>(bindContext, grpcMethod, stub);
            }
            catch (Exception ex)
            {
                OnError(ex.Message);
                return false;
            }

        }

        /// <summary>
        /// The implementing binder should bind the method to the bind-state
        /// </summary>
        protected abstract bool TryBind<TService, TRequest, TResponse>(ServiceBindContext bindContext, Method<TRequest, TResponse> method, MethodStub<TService> stub)
            where TService : class
            where TRequest : class
            where TResponse : class;

        void IBindContext.LogWarning(string message, object?[]? args) => OnWarn(message, args);
        void IBindContext.LogError(string message, object?[]? args) => OnError(message, args);

        /// <summary>
        /// Publish a warning message
        /// </summary>
        protected internal virtual void OnWarn(string message, object?[]? args = null) { }

        /// <summary>
        /// Publish a warning message
        /// </summary>
        protected internal virtual void OnError(string message, object?[]? args = null) { }

        /// <summary>
        /// Describes the relationship between a service contract and a service definition
        /// </summary>
        protected internal sealed class ServiceBindContext
        {
            /// <summary>
            /// The caller-provided state for this operation
            /// </summary>
            public object State { get; }
            /// <summary>
            /// The service contract interface type
            /// </summary>
            public Type ContractType { get; }
            /// <summary>
            /// The concrete service type
            /// </summary>
            public Type ServiceType { get; }

            private InterfaceMapping? _map;
            private InterfaceMapping GetMap() // lazily memoized
                => _map ??= ServiceType.GetInterfaceMap(ContractType);
            internal ServiceBindContext(Type contractType, Type serviceType, object state)
            {
                State = state;
                ContractType = contractType;
                ServiceType = serviceType;
            }

            /// <summary>
            /// Gets the implementing method from a method definition
            /// </summary>
            public MethodInfo? GetImplementation(MethodInfo serviceMethod)
            {
                if (ContractType != ServiceType & serviceMethod is object)
                {
                    var map = GetMap();
                    var from = map.InterfaceMethods;
                    var to = map.TargetMethods;
                    int end = Math.Min(from.Length, to.Length);
                    for (int i = 0; i < end; i++)
                    {
                        if (from[i] == serviceMethod) return to[i];
                    }
                }
                return null;
            }

            /// <summary>
            /// Gets the metadata associated with a specific contract method
            /// </summary>
            public List<object> GetMetadata(MethodInfo method)
            {
                // consider the various possible sources of distinct metadata
                object[]
                    contractType = ContractType.GetCustomAttributes(inherit: true),
                    contractMethod = method.GetCustomAttributes(inherit: true),
                    serviceType = Array.Empty<object>(),
                    serviceMethod = Array.Empty<object>();
                if (ContractType != ServiceType & ContractType.IsInterface & ServiceType.IsClass)
                {
                    serviceType = ServiceType.GetCustomAttributes(inherit: true);
                    serviceMethod = GetImplementation(method)?.GetCustomAttributes(inherit: true)
                        ?? Array.Empty<object>();
                }

                // note: later is higher priority in the code that consumes this, but
                // GetAttributes() is "most derived to least derived", so: add everything
                // backwards, then reverse
                var metadata = new List<object>(
                    contractType.Length + contractMethod.Length +
                    serviceType.Length + serviceMethod.Length);

                metadata.AddRange(serviceMethod);
                metadata.AddRange(serviceType);
                metadata.AddRange(contractMethod);
                metadata.AddRange(contractType);
                metadata.Reverse();
                return metadata;
            }
        }
    }

}
