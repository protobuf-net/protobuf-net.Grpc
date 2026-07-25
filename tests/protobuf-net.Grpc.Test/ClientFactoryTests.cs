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
            Assert.StartsWith("ProtoBuf.Grpc.Internal.Proxies.ClientBase.", name);
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
            Assert.StartsWith("ProtoBuf.Grpc.Internal.Proxies.ClientBase.", name);
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