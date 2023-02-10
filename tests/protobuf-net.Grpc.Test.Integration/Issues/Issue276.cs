using ProtoBuf;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Meta;
using System;
using System.Reflection;
using Xunit;

namespace protobuf_net.Grpc.Test.Issues
{
#if NET6_0_OR_GREATER // needs system type for init-only accessor
    public class Issue276
    {
        static readonly bool IsV3OrAbove = typeof(RuntimeTypeModel).Assembly.GetName().Version!.Major >= 3;

        [Theory]
        [InlineData(typeof(Foo))]
        [InlineData(typeof(Bar))]
        [InlineData(typeof(Baz))]
        public void CanSerializeNakedRecord(Type type)
        {
            Assert.Equal(IsV3OrAbove, CanSerialize(type));
        }

        public record Foo(Bar Bar, Baz Baz);
        public record Bar(int X, int Y);
        public record Baz(int Z);

        [Theory]
        [InlineData(typeof(Foo2))]
        [InlineData(typeof(Bar2))]
        [InlineData(typeof(Baz2))]
        public void CanSerializeAnnotatedRecord(Type type)
        {
            Assert.True(CanSerialize(type));
        }

        [ProtoContract(SkipConstructor = true)]
        public record Foo2([property: ProtoMember(1)] Bar2 Bar, [property: ProtoMember(2)] Baz2 Baz);
        [ProtoContract(SkipConstructor = true)]
        public record Bar2([property: ProtoMember(1)] int X, [property: ProtoMember(2)] int Y);

        [ProtoContract(SkipConstructor = true)]
        public record Baz2([property: ProtoMember(1)]int Z);


        // sneaky look under the covers to see what we can serialize
        private static bool CanSerialize(Type type, MarshallerFactory? marshaller = null)
    => (bool)_canSerialize.Invoke(marshaller ?? ProtoBufMarshallerFactory.Default, new object[] { type })!;

        private static readonly MethodInfo _canSerialize = typeof(MarshallerFactory)
            .GetMethod("CanSerialize", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("CanSerialize not found");
    }
#endif
}
