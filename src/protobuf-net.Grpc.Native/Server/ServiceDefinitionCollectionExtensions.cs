using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using static Grpc.Core.Server;

namespace ProtoBuf.Grpc.Server
{
    /// <summary>
    /// Provides extension methods to the ServiceDefinitionCollection API
    /// </summary>
    public static class ServiceDefinitionCollectionExtensions
    {
        private class Binder : ServerBinder
        {
            private Binder() { }
            public static readonly Binder Instance = new Binder();

            protected override bool OnBind<TService, TRequest, TResponse>(object state, Method<TRequest, TResponse> method, MethodStub stub, TService? service)
                where TService : class
                where TRequest : class
                where TResponse : class
            {
                var builder = (ServerServiceDefinition.Builder)state;
                switch (method.Type)
                {
                    case MethodType.Unary:
                        builder.AddMethod(method, stub.As<TService, UnaryServerMethod<TRequest, TResponse>>(service!));
                        break;
                    case MethodType.ClientStreaming:
                        builder.AddMethod(method, stub.As<TService, ClientStreamingServerMethod<TRequest, TResponse>>(service!));
                        break;
                    case MethodType.ServerStreaming:
                        builder.AddMethod(method, stub.As<TService, ServerStreamingServerMethod<TRequest, TResponse>>(service!));
                        break;
                    case MethodType.DuplexStreaming:
                        builder.AddMethod(method, stub.As<TService, DuplexStreamingServerMethod<TRequest, TResponse>>(service!));
                        break;
                    default:
                        return false;
                }
                return true;
            }
        }
        /// <summary>
        /// Adds a code-first service to the available services
        /// </summary>
        public static int AddCodeFirst<TService>(this ServiceDefinitionCollection services, TService service, BinderConfiguration? binderConfiguration = null)
        {
            var builder = ServerServiceDefinition.CreateBuilder();
            int count = Binder.Instance.Bind(builder, typeof(TService), binderConfiguration, service);
            services.Add(builder.Build());
            return count;
        }
    }
}
