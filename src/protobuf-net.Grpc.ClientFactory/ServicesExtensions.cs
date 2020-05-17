using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf.Grpc.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace ProtoBuf.Grpc.ClientFactory
{
    /// <summary>
    /// Provides extension methods to the IServiceCollection API
    /// </summary>
    public static class ServicesExtensions
    {
        /// <summary>
        /// Registers a provider that can recognize and handle code-first services
        /// </summary>
        public static IHttpClientBuilder AddCodeFirstGrpcClient<T>(this IServiceCollection services) where T : class
            => services.AddGrpcClient<T>().ConfigureCodeFirstGrpcClient<T>();

        /// <summary>
        /// Registers a provider that can recognize and handle code-first services
        /// </summary>
        public static IHttpClientBuilder AddCodeFirstGrpcClient<T>(this IServiceCollection services,
            string name) where T : class
            => services.AddGrpcClient<T>(name).ConfigureCodeFirstGrpcClient<T>();

        /// <summary>
        /// Registers a provider that can recognize and handle code-first services
        /// </summary>
        public static IHttpClientBuilder AddCodeFirstGrpcClient<T>(this IServiceCollection services,
            Action<GrpcClientFactoryOptions> configureClient) where T : class
            => services.AddGrpcClient<T>(configureClient).ConfigureCodeFirstGrpcClient<T>();

        /// <summary>
        /// Configures the provided client-builder to use code-first GRPC for client creation
        /// </summary>
        public static IHttpClientBuilder ConfigureCodeFirstGrpcClient<T>(this IHttpClientBuilder clientBuilder) where T : class
            => clientBuilder.ConfigureGrpcClientCreator(
                (services, callInvoker) => Client.GrpcClientFactory.CreateGrpcService<T>(callInvoker,
                    services.GetService<Configuration.ClientFactory>()));
    }
}
