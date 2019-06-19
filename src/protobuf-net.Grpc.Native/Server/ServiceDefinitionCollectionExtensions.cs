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
        /// <summary>
        /// Adds a code-first service to the available services
        /// </summary>
        public static int AddCodeFirst<TService>(this ServiceDefinitionCollection services, TService service, BinderConfiguration? binderConfiguration = null)
            where TService : class
        {
            var builder = ServerServiceDefinition.CreateBuilder();
            int count = Binder.Instance.Bind<TService>(builder, binderConfiguration, service);
            services.Add(builder.Build());
            return count;
        }

        private class Binder : ServerBinder
        {
            private Binder() { }
            public static readonly Binder Instance = new Binder();

            protected override bool TryBind<TService, TRequest, TResponse>(object state, Method<TRequest, TResponse> method, MethodStub<TService> stub)
                where TService : class
                where TRequest : class
                where TResponse : class
            {
                var builder = (ServerServiceDefinition.Builder)state;
                switch (method.Type)
                {
                    case MethodType.Unary:
                        builder.AddMethod(method, stub.CreateDelegate<UnaryServerMethod<TRequest, TResponse>>());
                        break;
                    case MethodType.ClientStreaming:
                        builder.AddMethod(method, stub.CreateDelegate<ClientStreamingServerMethod<TRequest, TResponse>>());
                        break;
                    case MethodType.ServerStreaming:
                        builder.AddMethod(method, stub.CreateDelegate<ServerStreamingServerMethod<TRequest, TResponse>>());
                        break;
                    case MethodType.DuplexStreaming:
                        builder.AddMethod(method, stub.CreateDelegate<DuplexStreamingServerMethod<TRequest, TResponse>>());
                        break;
                    default:
                        return false;
                }
                return true;
            }
        }
    }
}
