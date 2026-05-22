#if CLIENT_FACTORY

using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf.Grpc.ClientFactory;
using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;
using Xunit;
namespace protobuf_net.Grpc.Test
{
    public class ClientFactoryTests
    {
        [Fact]
        public void CanConfigureCodeFirstClient()
        {
            var services = new ServiceCollection();
            services.AddCodeFirstGrpcClient<IMyService>(o =>
            {
                o.Address = new Uri("http://localhost");
            });
            var serviceProvider = services.BuildServiceProvider();
            
            var clientFactory = serviceProvider.GetRequiredService<GrpcClientFactory>();
            var client = clientFactory.CreateClient<IMyService>(nameof(IMyService));

            Assert.NotNull(client);
            var name = client.GetType().FullName;
            // The source generator emits proxies in ProtoBuf.Grpc.Generated.* now that
            // [ServiceContract]/[Service] interfaces are auto-detected. The IL-emit fallback
            // (ProtoBuf.Grpc.Internal.Proxies.*) is only reached if no generated proxy exists.
            Assert.StartsWith("ProtoBuf.Grpc.Generated.", name);
        }

        [Fact]
        public void CanConfigureHttpClientBuilder()
        {
            var services = new ServiceCollection();
            services.AddGrpcClient<IMyService>(o =>
            {
                o.Address = new Uri("http://localhost");
            }).ConfigureCodeFirstGrpcClient<IMyService>();
            var serviceProvider = services.BuildServiceProvider();

            var clientFactory = serviceProvider.GetRequiredService<GrpcClientFactory>();
            var client = clientFactory.CreateClient<IMyService>(nameof(IMyService));

            Assert.NotNull(client);
            var name = client.GetType().FullName;
            // The source generator emits proxies in ProtoBuf.Grpc.Generated.* now that
            // [ServiceContract]/[Service] interfaces are auto-detected. The IL-emit fallback
            // (ProtoBuf.Grpc.Internal.Proxies.*) is only reached if no generated proxy exists.
            Assert.StartsWith("ProtoBuf.Grpc.Generated.", name);
        }
    }

    [ServiceContract]
    public interface IMyService
    {
        [OperationContract]
        public ValueTask<Dummy> UnaryCall(Dummy value);
    }

    [DataContract]
    public class Dummy { }
}
#endif