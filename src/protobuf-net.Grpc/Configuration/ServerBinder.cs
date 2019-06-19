using Grpc.Core;
using ProtoBuf.Grpc.Internal;
using System;
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
        public int Bind<TService>(object state, BinderConfiguration? binderConfiguration = null, TService? service = null)
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
            foreach (var serviceContract in ContractOperation.ExpandInterfaces(serviceType))
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
                OnServiceBound(state, serviceName, serviceContract, svcOpCount);
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
                        argsBuffer = new object?[] { serviceName, null, null, null, state, null, binderConfiguration!.MarshallerFactory, service };
                    }
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
        protected virtual void OnServiceBound(object state, string serviceName, Type serviceContract, int operationCount) { }

        private static readonly MethodInfo s_addMethod = typeof(ServerBinder).GetMethod(
            nameof(AddMethod), BindingFlags.Instance | BindingFlags.NonPublic)!;

        /// <summary>
        /// Provides utilities associated with the method being considered
        /// </summary>
        protected readonly struct MethodStub
        {
            private readonly Func<MethodInfo, Expression[], Expression>? _invoker;

            /// <summary>
            /// The runtime method being considered
            /// </summary>
            public MethodInfo Method { get; }

            internal MethodStub(Func<MethodInfo, Expression[], Expression>? invoker, MethodInfo method)
            {
                _invoker = invoker;
                Method = method;
            }

            /// <summary>
            /// Create a delegate that will invoke this method against a constant instance of the service
            /// </summary>
            public TDelegate As<TService, TDelegate>(TService service)
                where TDelegate : Delegate
                where TService : class
            {
                if (_invoker == null)
                {
                    // basic - direct call
                    return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), service, Method);
                }
                var finalSignature = typeof(TDelegate).GetMethod("Invoke")!;

                var methodParameters = finalSignature.GetParameters();
                Expression[] mapArgs = new Expression[methodParameters.Length + 1];
                var lambdaParameters = Array.ConvertAll(methodParameters, p => Expression.Parameter(p.ParameterType, p.Name));
                mapArgs[0] = Expression.Constant(service, typeof(TService));
                for (int i = 0; i < methodParameters.Length; i++) mapArgs[i + 1] = lambdaParameters[i];
                var body = _invoker?.Invoke(Method, mapArgs);
                var lambda = Expression.Lambda<TDelegate>(body, lambdaParameters);

                return lambda.Compile();
            }

            /// <summary>
            /// Create a delegate that will invoke this method against an instance of the service that is provided as the first argument
            /// </summary>
            public TDelegate As<TDelegate>() where TDelegate : Delegate
            {
                if (_invoker == null)
                {
                    // basic - direct call
                    return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), null, Method);
                }
                var finalSignature = typeof(TDelegate).GetMethod("Invoke")!;

                var methodParameters = finalSignature.GetParameters();
                var lambdaParameters = Array.ConvertAll(methodParameters, p => Expression.Parameter(p.ParameterType, p.Name));
                var body = _invoker?.Invoke(Method, lambdaParameters);
                var lambda = Expression.Lambda<TDelegate>(body, lambdaParameters);
                return lambda.Compile();
            }
        }

        private bool AddMethod<TService, TRequest, TResponse>(
            string serviceName, string operationName, MethodInfo method, MethodType methodType,
            object state,
            Func<MethodInfo, Expression[], Expression>? invoker, MarshallerFactory marshallerFactory, TService? service)
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            var grpcMethod = new Method<TRequest, TResponse>(methodType, serviceName, operationName, marshallerFactory.GetMarshaller<TRequest>(), marshallerFactory.GetMarshaller<TResponse>());
            var stub = new MethodStub(invoker, method);
            return OnBind<TService, TRequest, TResponse>(state, grpcMethod, stub, service);

        }

        /// <summary>
        /// The implementing binder should bind the method to the bind-state
        /// </summary>
        protected abstract bool OnBind<TService, TRequest, TResponse>(object state, Method<TRequest, TResponse> method, MethodStub stub, TService? service)
            where TService : class
            where TRequest : class
            where TResponse : class;

    }
}
