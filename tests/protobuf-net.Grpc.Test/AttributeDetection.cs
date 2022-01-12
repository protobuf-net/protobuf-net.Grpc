using System;
using System.Linq;
using System.Threading.Tasks;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Grpc.Internal;
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
            var ctx = new ServiceBindContext(typeof(ISomeService), typeof(SomeServer), "n/a", new ServiceBinder());
            var method = typeof(ISomeService).GetMethod(methodName)!;
            var actual = string.Join(",", ctx.GetMetadata(method).OfType<SomethingAttribute>().Select(x => x.Value));
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(typeof(SomeBaseType), true, "base", "base bar")]
        [InlineData(typeof(SomeMiddleType), true, "middle", "base bar")]
        [InlineData(typeof(SomeLeafType), true, "leaf", "base bar")]
        [InlineData(typeof(SomeBaseType), false, "base", "base bar")]
        [InlineData(typeof(SomeMiddleType), false, "middle", null)]
        [InlineData(typeof(SomeLeafType), false, "leaf", null)]
        public void CheckTypeAttributes(Type type, bool inherit, string expectedFoo, string expectedBar)
            => CheckResults(AttributeHelper.For(type, inherit), expectedFoo, expectedBar);
        private static void CheckResults(AttributeHelper attribs, string expectedFoo, string expectedBar)
        {
            var fooResult = attribs.TryGetAnyNonWhitespaceString(typeof(FooAttribute).FullName!, "Name", out var actualFoo);
            var barResult = attribs.TryGetAnyNonWhitespaceString(typeof(BarAttribute).FullName!, "Name", out var actualBar);

            if (string.IsNullOrWhiteSpace(expectedFoo))
            {
                Assert.False(fooResult);
            }
            else
            {
                Assert.True(fooResult);
                Assert.Equal(expectedFoo, actualFoo);
            }
            if (string.IsNullOrWhiteSpace(expectedBar))
            {
                Assert.False(barResult);
            }
            else
            {
                Assert.True(barResult);
                Assert.Equal(expectedBar, actualBar);
            }
        }

        [Theory]
        [InlineData(typeof(SomeBaseType), "A", true, "a base", "a base bar")]
        [InlineData(typeof(SomeBaseType), "B", true, "b base", "b base bar")]
        [InlineData(typeof(SomeBaseType), "C", true, "c base", "c base bar")]
        [InlineData(typeof(SomeMiddleType), "A", true, "a middle", "a base bar")]
        [InlineData(typeof(SomeMiddleType), "B", true, "b base", "b base bar")]
        [InlineData(typeof(SomeMiddleType), "C", true, "c middle", "c base bar")]
        [InlineData(typeof(SomeLeafType), "A", true, "a middle", "a base bar")]
        [InlineData(typeof(SomeLeafType), "B", true, "b leaf", "b base bar")]
        [InlineData(typeof(SomeLeafType), "C", true, "c leaf", "c base bar")]

        [InlineData(typeof(SomeBaseType), "A", false, "a base", "a base bar")]
        [InlineData(typeof(SomeBaseType), "B", false, "b base", "b base bar")]
        [InlineData(typeof(SomeBaseType), "C", false, "c base", "c base bar")]
        [InlineData(typeof(SomeMiddleType), "A", false, "a middle", null)]
        [InlineData(typeof(SomeMiddleType), "B", false, "b base", "b base bar")]
        [InlineData(typeof(SomeMiddleType), "C", false, "c middle", null)]
        [InlineData(typeof(SomeLeafType), "A", false, "a middle", null)]
        [InlineData(typeof(SomeLeafType), "B", false, "b leaf", null)]
        [InlineData(typeof(SomeLeafType), "C", false, "c leaf", null)]

        [InlineData(typeof(SomeBaseType), "D", true, null, null)]
        [InlineData(typeof(SomeMiddleType), "D", true, null, null)]
        [InlineData(typeof(SomeLeafType), "D", true, null, null)]
        [InlineData(typeof(SomeBaseType), "D", false, null, null)]
        [InlineData(typeof(SomeMiddleType), "D", false, null, null)]
        [InlineData(typeof(SomeLeafType), "D", false, null, null)]

        public void CheckMethodAttributes(Type type, string method, bool inherit, string expectedFoo, string expectedBar)
            => CheckResults(AttributeHelper.For(type.GetMethod(method)!, inherit), expectedFoo, expectedBar);

        [Foo("base")]
        [Bar("base bar")]
        public class SomeBaseType
        {
            [Foo("a base")]
            [Bar("a base bar")]
            public virtual void A() { }
            [Foo("b base")]
            [Bar("b base bar")]
            public virtual void B() { }
            [Foo("c base")]
            [Bar("c base bar")]
            public virtual void C() { }
        }
        [Foo("middle")]
        public class SomeMiddleType : SomeBaseType
        {
            [Foo("a middle")]
            public override void A() { }
            [Foo("c middle")]
            public override void C() { }
        }
        [Foo("leaf")]
        public class SomeLeafType : SomeMiddleType
        {
            [Foo("b leaf")]
            public override void B() { }
            [Foo("c leaf")]
            public override void C() { }
        }

        [AttributeUsage(AttributeTargets.All)]
        public class FooAttribute : Attribute
        {
            public string Name { get; set; }
            public FooAttribute() { Name = ""; }
            public FooAttribute(string name) { Name = name; }
        }

        [AttributeUsage(AttributeTargets.All)]
        public class BarAttribute : Attribute
        {
            public string Name { get; set; }
            public BarAttribute() { Name = ""; }
            public BarAttribute(string name) { Name = name; }
        }
    }



}
