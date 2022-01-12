using ProtoBuf.Grpc.Configuration;
using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using Xunit;

namespace protobuf_net.Grpc.Test.Issues
{
    public class SO61727870
    {
        [Theory]
        [InlineData(typeof(IFoo), "protobuf_net.Grpc.Test.Issues.Foo")]
        [InlineData(typeof(IFoo<X>), "protobuf_net.Grpc.Test.Issues.Foo_zzz")]
        [InlineData(typeof(IBar), "abc")]
        [InlineData(typeof(IBar<X>), "abc_zzz")]
        [InlineData(typeof(IBlap), "def")]
        [InlineData(typeof(IBlap<X>), "zzz_def")]
        public void Foo(Type type, string expected)
        {
            Assert.True(new ServiceBinder().IsServiceContract(type, out var actual));
            Assert.Equal(expected, actual);
        }

        [ServiceContract]
        public interface IFoo { }
        [Service]
        public interface IFoo<T> { }

        [ServiceContract(Name = "abc")]
        public interface IBar { }
        [ServiceContract(Name = "abc_{0}")]
        public interface IBar<T> { }

        [Service("def")]
        public interface IBlap { }
        [Service("{0}_def")]
        public interface IBlap<T> { }

        [DataContract(Name = "zzz")]
        public class X { }
    }
}
