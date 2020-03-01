using ProtoBuf.Grpc.Configuration;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using static ProtoBuf.Grpc.Configuration.ServerBinder;

namespace protobuf_net.Grpc.Test
{

    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
    public class SomethingAttribute : Attribute
    {
        public string Value { get; }
        public SomethingAttribute(string value)
            => Value = value;
    }

    [Something("Interface")]
    public interface ISomeService
    {
        [Something("ImplicitInterfaceMethod")]
        ValueTask Implicit();
        [Something("ExplicitInterfaceMethod")]
        ValueTask Explicit();
    }
    [Something("BaseType")]
    public class BaseService
    {
        [Something("ImplicitServiceMethodBase")]
        public virtual ValueTask Implicit() => default;

    }
    [Something("Service")]
    public class SomeServer : BaseService, ISomeService
    {
        [Something("ImplicitServiceMethodOverride")]
        public override ValueTask Implicit() => default;

        [Something("ExplicitServiceMethod")]
        ValueTask ISomeService.Explicit() => default;
    }

    public class AttributeDetection
    {
        [Theory]
        [InlineData(nameof(ISomeService.Implicit),
            "Interface,ImplicitInterfaceMethod,BaseType,Service,ImplicitServiceMethodBase,ImplicitServiceMethodOverride")]
        [InlineData(nameof(ISomeService.Explicit),
            "Interface,ExplicitInterfaceMethod,BaseType,Service,ExplicitServiceMethod")]
        public void AttributesDetectedWherever(string methodName, string expected)
        {
            var ctx = new ServiceBindContext(typeof(ISomeService), typeof(SomeServer), "n/a");
            var method = typeof(ISomeService).GetMethod(methodName)!;
            var actual = string.Join(",", ctx.GetMetadata(method).OfType<SomethingAttribute>().Select(x => x.Value));
            Assert.Equal(expected, actual);

        }
    }
}
