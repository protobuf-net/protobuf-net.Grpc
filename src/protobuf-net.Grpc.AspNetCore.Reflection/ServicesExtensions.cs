using Grpc.AspNetCore.Server;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProtoBuf.Grpc.Server
{
    /// <summary>
    /// Provides extension methods to the IServiceCollection API
    /// </summary>
    public static class ServicesExtensions
    {
        /// <summary>
        /// Adds gRPC reflection services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> for adding services.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddCodeFirstGrpcReflection(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            // ReflectionService is designed to be a singleton
            // Explicitly register creating it in DI using descriptors calculated from gRPC endpoints in the app
            services.TryAddSingleton<ReflectionService>(serviceProvider =>
            {
                var binderConfiguration = serviceProvider.GetService<BinderConfiguration>();
                var endpointDataSource = serviceProvider.GetRequiredService<EndpointDataSource>();

                var grpcEndpointMetadata = endpointDataSource.Endpoints
                    .Select(ep => ep.Metadata.GetMetadata<GrpcMethodMetadata>())
                    .Where(m => m != null)
                    .ToList();

                var serviceTypes = grpcEndpointMetadata.Select(m => m.ServiceType).Distinct().ToArray();

                return new ReflectionService(binderConfiguration, serviceTypes);
            });

            return services;
        }
    }
}