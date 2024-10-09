using Grpc.Core;
using Grpc.Core.Interceptors;
using ProtoBuf.Grpc.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ProtoBuf.Grpc
{
    /// <summary>
    /// Helper APIs for configuring services against a <see cref="ServiceBinderBase">service-binder</see>.
    /// </summary>
    public static class ServiceBinderBaseExtensions
    {
        /// <summary>
        /// Adds a code-first service to the available services
        /// </summary>
        public static int AddCodeFirst<TService>(this ServiceBinderBase binder, TService service,
            BinderConfiguration? binderConfiguration = null,
            TextWriter? log = null)
            where TService : class
            => AddCodeFirstImpl(binder, service, typeof(TService), binderConfiguration, log);

        /// <summary>
        /// Adds a code-first service to the available services
        /// </summary>
        public static int AddCodeFirst(this ServiceBinderBase binder, object service,
            BinderConfiguration? binderConfiguration = null,
            TextWriter? log = null)
            => AddCodeFirstImpl(binder, service, service?.GetType() ?? throw new ArgumentNullException(nameof(service)), binderConfiguration, log);

        private static int AddCodeFirstImpl(ServiceBinderBase binder, object service, Type serviceType,
            BinderConfiguration? binderConfiguration,
            TextWriter? log)
        {
            return ServerBinder.Create(log).Bind(binder, serviceType, binderConfiguration, service);
        }

        /// <summary>
        /// Attach endpoints to this instance, using the configuration from <see cref="BindServiceMethodAttribute"/> on the type.
        /// </summary>
        public static void Bind<T>(this ServiceBinderBase binder, T? server = null) where T : class
        {
            var binderAttrib = typeof(T).GetCustomAttribute<BindServiceMethodAttribute>(true)
                ?? throw new InvalidOperationException("No " + nameof(BindServiceMethodAttribute) + " found");
            if (binderAttrib.BindType is null) throw new InvalidOperationException("No " + nameof(BindServiceMethodAttribute) + "." + nameof(BindServiceMethodAttribute.BindType) + " found");

            var method = binderAttrib.BindType.FindMembers(MemberTypes.Method, BindingFlags.Public | BindingFlags.Static,
                static (member, state) =>
                {
                    if (member is not MethodInfo method) return false;
                    if (method.Name != (string)state!) return false;

                    if (method.ReturnType != typeof(void)) return false;
                    var args = method.GetParameters();
                    if (args.Length != 2) return false;
                    if (args[0].ParameterType != typeof(ServiceBinderBase)) return false;
                    if (!args[1].ParameterType.IsAssignableFrom(typeof(T))) return false;
                    return true;

                }, binderAttrib.BindMethodName).OfType<MethodInfo>().SingleOrDefault()
                ?? throw new InvalidOperationException("No suitable " + binderAttrib.BindType.Name + "." + binderAttrib.BindMethodName + " method found");
            server ??= Activator.CreateInstance<T>();
            method.Invoke(null, [binder, server]);
        }


        /// <summary>
        /// Apply interceptors to a binder.
        /// </summary>
        public static ServiceBinderBase Intercept(this ServiceBinderBase binder, Interceptor interceptor)
            => interceptor is null ? binder : new InterceptedBinder(binder, interceptor);

        /// <summary>
        /// Apply interceptors to a binder.
        /// </summary>
        public static ServiceBinderBase Intercept(this ServiceBinderBase binder, params Interceptor[] interceptors)
        {
            if (interceptors is null || interceptors.Length == 0) return binder; // nothing to do
            return interceptors.Length == 1 ? Intercept(binder, interceptors[0]) : new InterceptedBinder(binder, interceptors);
        }

        private sealed class InterceptedBinder : ServiceBinderBase
        {
            private readonly ServiceBinderBase _tail;
            private readonly object _singleOrArray;

            public InterceptedBinder(ServiceBinderBase tail, Interceptor interceptor)
            {
                // TODO: combine if the tail is InterceptedBinder 
                _tail = tail;
                _singleOrArray = interceptor;
            }
            public InterceptedBinder(ServiceBinderBase tail, Interceptor[] interceptors)
            {
                // TODO: combine if the tail is InterceptedBinder 
                _tail = tail;
                _singleOrArray = interceptors;
            }

            public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ClientStreamingServerMethod<TRequest, TResponse>? handler)
            {
                if (handler is not null)
                {
                    if (_singleOrArray is Interceptor single)
                    {
                        handler = WrapIfNeeded(single, handler);
                    }
                    else if (_singleOrArray is Interceptor[] arr)
                    {
                        for (int i = 0; i < arr.Length; i++)
                        {
                            handler = WrapIfNeeded(arr[i], handler);
                        }
                    }
                }
                _tail.AddMethod(method, handler);

                static ClientStreamingServerMethod<TRequest, TResponse> WrapIfNeeded(Interceptor interceptor, ClientStreamingServerMethod<TRequest, TResponse> handler)
                    => NeedsWrapping(interceptor, nameof(Interceptor.ClientStreamingServerHandler)) ? Wrap(interceptor, handler) : handler;
                static ClientStreamingServerMethod<TRequest, TResponse> Wrap(Interceptor interceptor, ClientStreamingServerMethod<TRequest, TResponse> handler)
                    => (requestStream, context) => interceptor.ClientStreamingServerHandler(requestStream, context, handler);
            }
            public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, DuplexStreamingServerMethod<TRequest, TResponse>? handler)
            {
                if (handler is not null)
                {
                    if (_singleOrArray is Interceptor single)
                    {
                        handler = WrapIfNeeded(single, handler);
                    }
                    else if (_singleOrArray is Interceptor[] arr)
                    {
                        for (int i = 0; i < arr.Length; i++)
                        {
                            handler = WrapIfNeeded(arr[i], handler);
                        }
                    }
                }
                _tail.AddMethod(method, handler);

                static DuplexStreamingServerMethod<TRequest, TResponse> WrapIfNeeded(Interceptor interceptor, DuplexStreamingServerMethod<TRequest, TResponse> handler)
                    => NeedsWrapping(interceptor, nameof(Interceptor.DuplexStreamingServerHandler)) ? Wrap(interceptor, handler) : handler;
                static DuplexStreamingServerMethod<TRequest, TResponse> Wrap(Interceptor interceptor, DuplexStreamingServerMethod<TRequest, TResponse> handler)
                    => (requestStream, responseStream, context) => interceptor.DuplexStreamingServerHandler(requestStream, responseStream, context, handler);
            }
            public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ServerStreamingServerMethod<TRequest, TResponse>? handler)
            {
                if (handler is not null)
                {
                    if (_singleOrArray is Interceptor single)
                    {
                        handler = WrapIfNeeded(single, handler);
                    }
                    else if (_singleOrArray is Interceptor[] arr)
                    {
                        for (int i = 0; i < arr.Length; i++)
                        {
                            handler = WrapIfNeeded(arr[i], handler);
                        }
                    }
                }
                _tail.AddMethod(method, handler);

                static ServerStreamingServerMethod<TRequest, TResponse> WrapIfNeeded(Interceptor interceptor, ServerStreamingServerMethod<TRequest, TResponse> handler)
                    => NeedsWrapping(interceptor, nameof(Interceptor.ServerStreamingServerHandler)) ? Wrap(interceptor, handler) : handler;
                static ServerStreamingServerMethod<TRequest, TResponse> Wrap(Interceptor interceptor, ServerStreamingServerMethod<TRequest, TResponse> handler)
                    => (request, responseStream, context) => interceptor.ServerStreamingServerHandler(request, responseStream, context, handler);
            }
            public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse>? handler)
            {
                if (handler is not null)
                {
                    if (_singleOrArray is Interceptor single)
                    {
                        handler = WrapIfNeeded(single, handler);
                    }
                    else if (_singleOrArray is Interceptor[] arr)
                    {
                        for (int i = 0; i < arr.Length; i++)
                        {
                            handler = WrapIfNeeded(arr[i], handler);
                        }
                    }
                }
                _tail.AddMethod(method, handler);

                static UnaryServerMethod<TRequest, TResponse> WrapIfNeeded(Interceptor interceptor, UnaryServerMethod<TRequest, TResponse> handler)
                    => NeedsWrapping(interceptor, nameof(Interceptor.UnaryServerHandler)) ? Wrap(interceptor, handler) : handler;
                static UnaryServerMethod<TRequest, TResponse> Wrap(Interceptor interceptor, UnaryServerMethod<TRequest, TResponse> handler)
                    => (request, context) => interceptor.UnaryServerHandler(request, context, handler);
            }

            private static bool NeedsWrapping(object obj, string methodName)
            {
                if (obj is null) return false; // nothing to wrap
                try
                {
                    var objType = obj.GetType();
                    var method = objType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    // if we can't find the method: assume the worst!
                    return method is null || method.DeclaringType == objType;
                }
                catch
                {
                    // anything goes wrong? just treat as overridden
                    return true;
                }
            }
        }
    }
}
