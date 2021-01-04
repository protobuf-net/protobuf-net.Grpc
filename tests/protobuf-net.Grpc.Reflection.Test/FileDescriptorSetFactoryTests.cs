using ProtoBuf;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Reflection;
using System;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Xunit;

namespace protobuf_net.Grpc.Reflection.Test
{
    public class FileDescriptorSetFactoryTests
    {
        [Fact]
        public void SimpleService()
        {
            var fileDescriptorSet = FileDescriptorSetFactory.Create(new[] { typeof(GreeterService) });

            Assert.Empty(fileDescriptorSet.GetErrors());
            Assert.Equal(new[] { "GreeterService" },
                fileDescriptorSet.Files.SelectMany(x => x.Services).Select(x => x.Name).ToArray());
            Assert.Equal(new[] { "HelloReply", "HelloRequest" },
                fileDescriptorSet.Files.SelectMany(x => x.MessageTypes).Select(x => x.Name).ToArray());
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

    [Service]
    public interface IGreeterService
    {
        ValueTask<HelloReply> SayHello(HelloRequest request, CallContext context);
    }

    public class GreeterService : IGreeterService
    {
        public ValueTask<HelloReply> SayHello(HelloRequest request, CallContext context) => throw new NotImplementedException();
    }
}
