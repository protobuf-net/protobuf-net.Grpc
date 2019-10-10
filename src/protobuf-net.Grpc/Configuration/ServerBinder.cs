using Grpc.Core;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Helper API for binding servers; this is an advanced API that would only be used if you are implementing a new transport provider
    /// </summary>
    public abstract class ServerBinder
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
#pragma warning disable CS8625
        public int Bind<TService>(object state, BinderConfiguration? binderConfiguration = null, TService service = null)
#pragma warning restore CS8625                                                                // TService? - but: compiler bug in preview6
            where TService : class
            => Bind(state, typeof(TService), binderConfiguration, service);

        /// <summary>
        /// Initiate a bind operation, causing all service methods to be crawled for the provided type
        /// </summary>
        public int Bind(object state, Type serviceType, BinderConfiguration? binderConfiguration = null, object? service = null)
        {
            if (binderConfiguration == null) binderConfiguration = BinderConfiguration.Default;

            int totalCount = 0;
            object?[]? argsBuffer = null;
            Type[] typesBuffer = Array.Empty<Type>();
            string? serviceName;
            if (binderConfiguration == null) binderConfiguration = BinderConfiguration.Default;
            var serviceContracts = typeof(IGrpcService).IsAssignableFrom(serviceType)
                ? new HashSet<Type> { serviceType }
                : ContractOperation.ExpandInterfaces(serviceType);

            foreach (var serviceContract in serviceContracts)
            {
                if (!binderConfiguration.Binder.IsServiceContract(serviceContract, out serviceName)) continue;

                int svcOpCount = 0;
                foreach (var op in ContractOperation.FindOperations(binderConfiguration, serviceContract))
                {
                    if (ServerInvokerLookup.TryGetValue(op.MethodType, op.Context, op.Result, op.Void, out var invoker)
                        && AddMethod(op.From, op.To, op.Name, op.Method, op.MethodType, invoker))
                    {
                        // yay!
                        totalCount++;
                        svcOpCount++;
                    }
                }
                OnServiceBound(state, serviceName!, serviceType, serviceContract, svcOpCount);
            }
            return totalCount;

            bool AddMethod(Type @in, Type @out, string on, MethodInfo m, MethodType t, Func<MethodInfo, ParameterExpression[], Expression>? invoker)
            {
                try
                {
                    if (typesBuffer.Length == 0)
                    {
                        typesBuffer = new Type[] { serviceType, typeof(void), typeof(void) };
                    }
                    typesBuffer[1] = @in;
                    typesBuffer[2] = @out;

                    if (argsBuffer == null)
                    {
                        argsBuffer = new object?[] { null, null, null, null, state, null, binderConfiguration!.MarshallerCache, service == null ? null : Expression.Constant(service, serviceType) };
                    }
                    argsBuffer[0] = serviceName;
                    argsBuffer[1] = on;
                    argsBuffer[2] = m;
                    argsBuffer[3] = t;
                    argsBuffer[5] = invoker;

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

            /// <summary>
            /// The runtime method being considered
            /// </summary>
            public MethodInfo Method { get; }

            internal MethodStub(Func<MethodInfo, Expression[], Expression>? invoker, MethodInfo method, ConstantExpression? service)
            {
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
                    // basic - direct call
                    return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), _service, Method);
                }
                var lambdaArgs = ParameterCache<TDelegate>.Parameters;

                Expression[] mapArgs;
                if (_service == null)
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
                var lambda = Expression.Lambda<TDelegate>(body, lambdaArgs);

                return lambda.Compile();
            }
        }

        private bool AddMethod<TService, TRequest, TResponse>(
            string serviceName, string operationName, MethodInfo method, MethodType methodType,
            object state,
            Func<MethodInfo, Expression[], Expression>? invoker, MarshallerCache marshallerCache,
            ConstantExpression? service)
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            var grpcMethod = new Method<TRequest, TResponse>(methodType, serviceName, operationName, marshallerCache.GetMarshaller<TRequest>(), marshallerCache.GetMarshaller<TResponse>());
            var stub = new MethodStub<TService>(invoker, method, service);
            return TryBind<TService, TRequest, TResponse>(state, grpcMethod, stub);

        }

        /// <summary>
        /// The implementing binder should bind the method to the bind-state
        /// </summary>
        protected abstract bool TryBind<TService, TRequest, TResponse>(object state, Method<TRequest, TResponse> method, MethodStub<TService> stub)
            where TService : class
            where TRequest : class
            where TResponse : class;

    }
}
