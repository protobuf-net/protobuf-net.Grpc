using automarshaller;
using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using System;
using Xunit;

namespace protobuf_net.Grpc.Test
{
    public class AutoMarshaller
    {

        /* from:
syntax = "proto3";
option csharp_namespace = "automarshaller";
message Foo {
  int32 value = 1;
}
         */

        public class UnknownType { }
        [Fact]
        public void GetMarshallerOnUnknownTypeFailsInExpectedWay()
        {
            Assert.False(BinderConfiguration.Default.MarshallerCache.CanSerializeType(typeof(UnknownType)));
            var ex = Assert.Throws<InvalidOperationException>(
                () =>
                {
                    BinderConfiguration.Default.GetMarshaller<UnknownType>();
                });
            Assert.Equal("No marshaller available for protobuf_net.Grpc.Test.AutoMarshaller+UnknownType", ex.Message);
        }

        [Fact]
        public void CanAutoDetectProtobufMarshaller()
        {
            var sctx = new TestSerializationContext();
            Assert.True(BinderConfiguration.Default.MarshallerCache.CanSerializeType(typeof(Foo)));
            var marshaller = BinderConfiguration.Default.GetMarshaller<Foo>();

            marshaller.ContextualSerializer(new Foo { Value = 42 }, sctx);
            var hex = BitConverter.ToString(sctx.Payload);
            Assert.Equal("08-2A", hex);
            var dctx = new TestDeserializationContext(sctx.Payload);
            var obj = marshaller.ContextualDeserializer(dctx);
            Assert.Equal(42, obj.Value);
        }
        class TestSerializationContext : SerializationContext
        {
            public byte[] Payload { get; set; } = Array.Empty<byte>();
            public override void Complete(byte[] payload) => Payload = payload;
        }
        internal class TestDeserializationContext : DeserializationContext
        {
            private byte[] _payload;

            public TestDeserializationContext(byte[] payload) => _payload = payload;

            public override int PayloadLength => _payload.Length;
            public override byte[] PayloadAsNewBuffer() => _payload;
        }
    }
}
