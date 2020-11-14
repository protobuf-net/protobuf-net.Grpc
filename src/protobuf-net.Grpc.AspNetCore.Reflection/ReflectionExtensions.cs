using Grpc.AspNetCore.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Reflection;
using System;
using System.Linq;

namespace ProtoBuf.Grpc.Server
{
    /// <summary>
    /// Provides extension methods to provide protobuf-net.Grpc reflection services
    /// </summary>
    public static class ReflectionExtensions
    {
        /// <summary>
        /// Maps incoming requests to the gRPC reflection service.
        /// This service can be queried to discover the gRPC services on the server.
        /// </summary>
        /// <param name="builder">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
        /// <returns>An <see cref="IEndpointConventionBuilder"/> for endpoints associated with the service.</returns>
        public static IEndpointConventionBuilder MapCodeFirstGrpcReflectionService(this IEndpointRouteBuilder builder)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.MapGrpcService<ReflectionService>();
        }

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
            services.TryAddSingleton(serviceProvider =>
            {
                var binderConfiguration = serviceProvider.GetService<BinderConfiguration>();
                var endpointDataSource = serviceProvider.GetRequiredService<EndpointDataSource>();

                var grpcEndpointMetadata = endpointDataSource.Endpoints
                    .Select(ep => ep.Metadata.GetMetadata<GrpcMethodMetadata>())
                    .Where(m => m?.ServiceType is object)
                    .ToList();

                var serviceTypes = grpcEndpointMetadata.Select(m => m!.ServiceType).Distinct().ToArray();

                return new ReflectionService(binderConfiguration, serviceTypes);
            });

            return services;
        }
    }
}