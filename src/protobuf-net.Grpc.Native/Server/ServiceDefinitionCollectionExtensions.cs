using Grpc.Core;
using Grpc.Core.Interceptors;
using ProtoBuf.Grpc.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
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
        public static int AddCodeFirst<TService>(ServiceDefinitionCollection services, TService service,
            BinderConfiguration? binderConfiguration,
            TextWriter? log)
            where TService : class // forwarded to preserve older API
            => AddCodeFirst<TService>(services, service, binderConfiguration, log, null);

        /// <summary>
        /// Adds a code-first service to the available services
        /// </summary>
        public static int AddCodeFirst<TService>(this ServiceDefinitionCollection services, TService service,
            BinderConfiguration? binderConfiguration = null,
            TextWriter? log = null,
            IEnumerable<Interceptor>? interceptors = null)
            where TService : class
            => AddCodeFirstImpl(services, service, typeof(TService), binderConfiguration, log, interceptors);

        /// <summary>
        /// Adds a code-first service to the available services
        /// </summary>
        public static int AddCodeFirst(this ServiceDefinitionCollection services, object service,
            BinderConfiguration? binderConfiguration = null,
            TextWriter? log = null,
            IEnumerable<Interceptor>? interceptors = null)
            => AddCodeFirstImpl(services, service, service?.GetType() ?? throw new ArgumentNullException(nameof(service)), binderConfiguration, log, interceptors);

        private static int AddCodeFirstImpl(ServiceDefinitionCollection services, object service, Type serviceType,
            BinderConfiguration? binderConfiguration,
            TextWriter? log,
            IEnumerable<Interceptor>? interceptors)
        {
            var builder = ServerServiceDefinition.CreateBuilder();
            int count = ServerBinder.Create(log).Bind(builder, serviceType, binderConfiguration, service);
            var serverServiceDefinition = builder.Build();
            
            if (interceptors is object)
            {
                foreach(var interceptor in interceptors)
                {
                    serverServiceDefinition = serverServiceDefinition.Intercept(interceptor);
                }
            }

            services.Add(serverServiceDefinition);
            return count;
        }
    }
}
