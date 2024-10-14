using Grpc.Core;
using ProtoBuf.Grpc.Internal;
using System;
using System.Buffers;
using System.IO;
using Xunit;

namespace protobuf_net.Grpc.Test;

#pragma warning disable CS0618 // all marked obsolete!

public class BytesValueMarshallerTests
{
    [Fact]
    public void ProveMaxLength()
    {
        Assert.Equal(0b1111111_1111111_1111111, BytesValue.MaxLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(32)]
    [InlineData(64)]
    // "varint" is a 7-bit scheme; easiest way to see
    // ranges is via 0b notation with 7-bit groups
    [InlineData(0b0000000_0000000_1111111)]
    [InlineData(0b0000000_0000001_0000000)]
    [InlineData(0b0000000_0000001_1111111)]
    [InlineData(0b0000000_1111111_1111111)]
    [InlineData(0b0000001_0000000_0000000)]
    [InlineData(0b1111011_0000000_0000000)]
    [InlineData(0b1111011_0000000_1010101)]
    [InlineData(0b1111011_1110101_1010101)]
    [InlineData(0b1111111_1111111_1111111)]

    public void TestFastParseAndFormat(int length)
    {
        var source = new byte[length];
        new Random().NextBytes(source);
        var ser = new TestSerializationContext();
        BytesValue.Marshaller.ContextualSerializer(new BytesValue(source, source.Length, false), ser);
        byte[] chunk = ser.ToArray();

#if DEBUG
        var missCount = BytesValue.FastPassMiss;
#endif

        // check via our custom deserializer
        var result = BytesValue.Marshaller.ContextualDeserializer(new TestDeserializationContext(chunk));
        Assert.NotNull(result);
        Assert.True(result.Span.SequenceEqual(source));
        Assert.True(result.IsPooled);
        Assert.False(result.IsRecycled);
        result.Recycle();
        Assert.False(result.IsPooled);
        Assert.True(result.IsRecycled);
        Assert.True(result.IsEmpty);

#if DEBUG
        Assert.Equal(missCount, BytesValue.FastPassMiss); // expect no new misses
#endif

        // and double-check via protobuf-net directly
        result = ProtoBuf.Serializer.Deserialize<BytesValue>(new MemoryStream(chunk));
        Assert.NotNull(result);
        Assert.True(result.Span.SequenceEqual(source));
        Assert.False(result.IsPooled);
        Assert.False(result.IsRecycled);
        result.Recycle();
        Assert.False(result.IsPooled);
        Assert.True(result.IsRecycled);
        Assert.True(result.IsEmpty);

    }

    class TestSerializationContext : SerializationContext
    {
        public byte[] ToArray() => _payload;
        private byte[] _payload = [];
        private TestBufferWriter? _writer;
        public override IBufferWriter<byte> GetBufferWriter() => _writer ?? new(_payload);

        public override void SetPayloadLength(int payloadLength)
        {
            Array.Resize(ref _payload, payloadLength);
            _writer = null;
        }

        public override void Complete() { }
        public override void Complete(byte[] payload) => _payload = payload;
    }

    class TestDeserializationContext(byte[] chunk) : DeserializationContext
    {
        public override int PayloadLength => chunk.Length;
        public override byte[] PayloadAsNewBuffer()
        {
            var arr = new byte[chunk.Length];
            Buffer.BlockCopy(chunk, 0, arr, 0, chunk.Length);
            return arr;
        }
        public override ReadOnlySequence<byte> PayloadAsReadOnlySequence()
            => new(chunk);
    }

    class TestBufferWriter(byte[] payload) : IBufferWriter<byte>
    {
        private byte[] _bytes = payload;
        private int _committed = 0;

        public void Advance(int count)
            => _committed += count;

        public byte[] AsArray() => _bytes;

        public Memory<byte> GetMemory(int sizeHint = 0)
            => new(_bytes, _committed, _bytes.Length - _committed);

        public Span<byte> GetSpan(int sizeHint = 0)
            => new(_bytes, _committed, _bytes.Length - _committed);
    }
}
