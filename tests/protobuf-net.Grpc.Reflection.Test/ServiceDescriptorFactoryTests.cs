using ProtoBuf;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Reflection;
using System;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Xunit;

namespace protobuf_net.Grpc.Reflection.Test
{
    public class ServiceDescriptorFactoryTests
    {
        [Fact]
        public void SimpleService()
        {
            var serviceDescriptors = ServiceDescriptorFactory.Instance.GetServiceDescriptors(typeof(GreeterService)).ToList();

            Assert.Single(serviceDescriptors);
        }
    }

    [ProtoContract]
    public class HelloRequest
    {
        [ProtoMember(1)]
        public string Name { get; set; } = string.Empty;
    }

    [ProtoContract]
    public class HelloReply
    {
        [ProtoMember(1)]
        public string Message { get; set; } = string.Empty;
    }

    [ServiceContract]
    public interface IGreeterService
    {
        ValueTask<HelloReply> SayHello(HelloRequest request, CallContext context);
    }

    public class GreeterService : IGreeterService
    {
        public ValueTask<HelloReply> SayHello(HelloRequest request, CallContext context) => throw new NotImplementedException();
    }
}
