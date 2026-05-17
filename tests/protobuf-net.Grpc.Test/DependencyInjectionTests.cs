#if !NETFRAMEWORK
using System.ServiceModel;
using Microsoft.Extensions.DependencyInjection;
using ProtoBuf;
using ProtoBuf.Grpc.ClientFactory;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Meta;
using Xunit;

namespace protobuf_net.Grpc.Test;

public class DependencyInjectionTests
{
    // [Fact] // WIP
    public void DependencyInjection()
    {
        ServiceCollection services = new();
        Register(services);
        services.AddCodeFirstGrpcClient<IMyService>();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<Foo>();
        
    }
    static void Register(IServiceCollection services)
    {
        var model = RuntimeTypeModel.Create();
        // manually configured, to prove working
        model.Add(typeof(Foo), false).AddField(1, nameof(Foo.Bar));

        var marshallerFactory = ProtoBufMarshallerFactory.Create(model, ProtoBufMarshallerFactory.Options.None);
        var binderConfiguration = BinderConfiguration.Create([marshallerFactory]);

        services.AddSingleton(binderConfiguration);
    }

    [ServiceContract]
    public interface IMyService
    {
        [OperationContract]
        Foo GetFoo();
    }
    public sealed class Foo
    {
        public int Bar { get; set; }
    }
}
#endif
