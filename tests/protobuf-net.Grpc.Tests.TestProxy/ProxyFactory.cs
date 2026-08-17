using System.Reflection;
using System.Runtime.Loader;
using ProtoBuf.Grpc.ClientFactory;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;

namespace protobuf_net.Grpc.Tests.TestProxy;
public static class ProxyFactory
{
    public static IProxy Create()
    {
        var services = new ServiceCollection();
        services.AddGrpcClient<IProxy>(o =>
        {
            o.Address = new Uri("http://localhost");
        }).ConfigureCodeFirstGrpcClient<IProxy>();
        var serviceProvider = services.BuildServiceProvider();

        var clientFactory = serviceProvider.GetRequiredService<GrpcClientFactory>();
        return clientFactory.CreateClient<IProxy>(nameof(IProxy));
    }
}
