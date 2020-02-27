using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ProtoBuf.Grpc.Reflection;

namespace ProtoBuf.Grpc.Server
{
    /// <summary>
    /// Provides extension methods for <see cref="IEndpointRouteBuilder"/> to add gRPC service endpoints.
    /// </summary>
    public static class EndpointRouteBuilderExtensions
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
    }
}
