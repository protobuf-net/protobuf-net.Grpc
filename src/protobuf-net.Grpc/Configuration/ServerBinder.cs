using Grpc.Core;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

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

            foreach (var serviceContract in serviceContracts)
            {
                if (!binderConfiguration.Binder.IsServiceContract(serviceContract, out serviceName)) continue;

                int svcOpCount = 0;
                var bindCtx = new ServiceBindContext(serviceContract, serviceType, state);
                foreach (var op in ContractOperation.FindOperations(binderConfiguration, serviceContract, this))
                {
                    if (ServerInvokerLookup.TryGetValue(op.MethodType, op.Context, op.Result, op.Void, out var invoker)
                        && AddMethod(op.From, op.To, op.Name, op.Method, op.MethodType, invoker, bindCtx))
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
                Func<MethodInfo, ParameterExpression[], Expression>? invoker, ServiceBindContext bindContext)
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
                        argsBuffer = new object?[] { null, null, null, null, null, null, binderConfiguration!.MarshallerCache, service == null ? null : Expression.Constant(service, serviceType) };
                    }
                    argsBuffer[0] = serviceName;
                    argsBuffer[1] = on;
                    argsBuffer[2] = m;
                    argsBuffer[3] = t;
                    argsBuffer[4] = bindContext;
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
            ServiceBindContext bindContext,
            Func<MethodInfo, Expression[], Expression>? invoker, MarshallerCache marshallerCache,
            ConstantExpression? service)
            where TService : class
            where TRequest : class
            where TResponse : class
        {
            var grpcMethod = new Method<TRequest, TResponse>(methodType, serviceName, operationName, marshallerCache.GetMarshaller<TRequest>(), marshallerCache.GetMarshaller<TResponse>());
            var stub = new MethodStub<TService>(invoker, method, service);
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
            public object State { get; }
            /// <summary>
            /// The service contract interface type
            /// </summary>
            public Type ContractType => _map.InterfaceType;
            /// <summary>
            /// The concrete service type
            /// </summary>
            public Type ServiceType => _map.TargetType;

            private readonly InterfaceMapping _map;
            internal ServiceBindContext(Type contractType, Type serviceType, object state)
            {
                State = state;
                _map = serviceType.GetInterfaceMap(contractType);
            }

            /// <summary>
            /// Gets the implementing method from a method definition
            /// </summary>
            public MethodInfo GetImplementation(MethodInfo serviceMethod)
            {
                var from = _map.InterfaceMethods;
                var to = _map.TargetMethods;
                int end = Math.Min(from.Length, to.Length);
                for (int i = 0; i < end; i++)
                {
                    if (from[i] == serviceMethod) return to[i];
                }
                throw new ArgumentException(nameof(serviceMethod));
            }

            /// <summary>
            /// Gets the metadata associated with a specific contract method
            /// </summary>
            public List<object> GetMetadata(MethodInfo method)
            {
                // note: later is higher priority in the code that consumes this, so
                // work upwards
                var metadata = new List<object>();
                // service contract - IFoo
                metadata.AddRange(ContractType.GetCustomAttributes(inherit: true));

                // service type - SomeService : IFoo
                metadata.AddRange(ServiceType.GetCustomAttributes(inherit: true));

                // service contract method: IFoo.Bar
                metadata.AddRange(method.GetCustomAttributes(inherit: true));

                // service type method: SomeService.Bar
                metadata.AddRange(GetImplementation(method).GetCustomAttributes(inherit: true));

                return metadata;
            }
        }
    }

}
