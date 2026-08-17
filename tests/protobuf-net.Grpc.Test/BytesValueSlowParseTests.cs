using Grpc.Core;
using ProtoBuf.Grpc.Internal;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace protobuf_net.Grpc.Test;

#pragma warning disable CS0618 // all marked obsolete!

/// <summary>
/// Serializes the BytesValue suites against each other.
/// </summary>
/// <remarks>
/// <c>BytesValue.FastPassMiss</c> is a process-wide DEBUG counter, and
/// <c>BytesValueMarshallerTests.TestFastParseAndFormat</c> asserts that it does not move across its
/// own round-trip. The slow-parse suite exists precisely to *miss* the fast path, so run in parallel
/// (xUnit's default across classes) it makes that assertion flap. Note this only bites in DEBUG,
/// which is why a Release-configuration run does not show it.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class BytesValueCollection
{
    public const string Name = "BytesValue";
}

/// <summary>
/// Covers the payloads <c>BytesValue.TryFastParse</c> declines, which is everything the hand-written
/// slow path is for.
/// </summary>
/// <remarks>
/// These used to be handled by <c>RuntimeTypeModel.Default</c>. That was replaced because it made the
/// whole reflective serializer machinery statically reachable from every consumer - see the remarks
/// on <c>SlowParse</c> - so this suite exists to show the replacement is equivalent, including for
/// the shapes only a non-protobuf-net peer would send.
/// </remarks>
[Collection(BytesValueCollection.Name)]
public class BytesValueSlowParseTests
{
    private static BytesValue Parse(params byte[] payload)
        => BytesValue.Marshaller.ContextualDeserializer(new SingleSegmentContext(payload));

    /// <summary>Field 1, length-delimited, with the given contents.</summary>
    private static IEnumerable<byte> Field1(params byte[] value)
    {
        yield return 0x0A;
        yield return checked((byte)value.Length); // callers here stay under 128 bytes
        foreach (var b in value) yield return b;
    }

    [Fact]
    public void EmptyPayloadIsAnEmptyValue()
    {
        // a BytesValue with everything defaulted encodes as zero bytes - legal protobuf, and one of
        // the ways TryFastParse declines (it reads four zero bytes and matches no case)
        var result = Parse();
        Assert.NotNull(result);
        Assert.True(result.IsEmpty);
        Assert.Equal(0, result.Length);
        Assert.False(result.IsPooled);
    }

    [Fact]
    public void ExplicitEmptyBytesIsAnEmptyValue()
    {
        var result = Parse(0x0A, 0x00);
        Assert.NotNull(result);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void UnknownFieldBeforePayloadIsSkipped()
    {
        // field 2, varint, value 42 - then our field; the leading byte is not 0x0A, so fast parse declines
        var payload = new byte[] { 0x10, 0x2A }.Concat(Field1(1, 2, 3)).ToArray();
        var result = Parse(payload);
        Assert.True(result.Span.SequenceEqual(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void UnknownFieldAfterPayloadIsSkipped()
    {
        // fast parse declines this one on length: header + bytes != payload length
        var payload = Field1(4, 5, 6).Concat(new byte[] { 0x10, 0x2A }).ToArray();
        var result = Parse(payload);
        Assert.True(result.Span.SequenceEqual(new byte[] { 4, 5, 6 }));
    }

    [Theory]
    [InlineData(1, 8)]   // fixed64
    [InlineData(5, 4)]   // fixed32
    public void UnknownFixedWidthFieldsAreSkipped(int wireType, int width)
    {
        var tag = (byte)((2 << 3) | wireType);
        var payload = new[] { tag }.Concat(new byte[width])
            .Concat(Field1(7)).ToArray();
        var result = Parse(payload);
        Assert.True(result.Span.SequenceEqual(new byte[] { 7 }));
    }

    [Fact]
    public void UnknownLengthDelimitedFieldIsSkipped()
    {
        // field 2, length-delimited, 3 bytes of junk
        var payload = new byte[] { 0x12, 0x03, 0xFF, 0xFF, 0xFF }.Concat(Field1(9)).ToArray();
        var result = Parse(payload);
        Assert.True(result.Span.SequenceEqual(new byte[] { 9 }));
    }

    [Fact]
    public void UnknownGroupThrows()
    {
        // Pins a deliberate limit rather than an aspiration. The runtime model would have skipped
        // this; supporting it costs recursion and a depth counter for a payload nobody can produce,
        // because BytesValue is .google.protobuf.BytesValue - frozen, one field, no schema evolution
        // that could introduce a group. If that ever stops being true, this test is the tripwire.
        var payload = new byte[] { 0x13, 0x18, 0x2A, 0x14 }.Concat(Field1(11)).ToArray();
        Assert.ThrowsAny<Exception>(() => Parse(payload));
    }

    [Fact]
    public void RepeatedFieldOneReplaces()
    {
        // The protobuf rule for a non-repeated field is "last one wins". Note the runtime model would
        // have *concatenated* these, via protobuf-net's append behaviour for byte[] - this is the one
        // deliberate behaviour change, and it is unreachable from anything we write, because each
        // chunk is its own gRPC message and Serialize emits field 1 exactly once.
        var payload = Field1(1, 2, 3).Concat(Field1(9, 9)).ToArray();
        var result = Parse(payload);
        Assert.True(result.Span.SequenceEqual(new byte[] { 9, 9 }));
        Assert.Equal(2, result.Length);
    }

    [Fact]
    public void MultiSegmentPayloadIsLinearized()
    {
        // splits the payload mid-field, which is the case the single-span parser cannot see directly
        var payload = new byte[] { 0x10, 0x2A }.Concat(Field1(1, 2, 3, 4, 5)).ToArray();
        var result = BytesValue.Marshaller.ContextualDeserializer(
            new MultiSegmentContext(payload, chunkSize: 3));
        Assert.True(result.Span.SequenceEqual(new byte[] { 1, 2, 3, 4, 5 }));
    }

    [Theory]
    [InlineData(new byte[] { 0x0A })]              // truncated: length prefix missing
    [InlineData(new byte[] { 0x0A, 0x05, 1, 2 })]  // truncated: fewer bytes than declared
    [InlineData(new byte[] { 0x14 })]              // end-group with no start
    [InlineData(new byte[] { 0x0F })]              // wire type 7 does not exist
    public void MalformedPayloadsThrow(byte[] payload)
        => Assert.ThrowsAny<Exception>(() => Parse(payload));

    private sealed class SingleSegmentContext(byte[] chunk) : DeserializationContext
    {
        public override int PayloadLength => chunk.Length;
        public override byte[] PayloadAsNewBuffer() => (byte[])chunk.Clone();
        public override ReadOnlySequence<byte> PayloadAsReadOnlySequence() => new(chunk);
    }

    private sealed class MultiSegmentContext(byte[] chunk, int chunkSize) : DeserializationContext
    {
        public override int PayloadLength => chunk.Length;
        public override byte[] PayloadAsNewBuffer() => (byte[])chunk.Clone();

        public override ReadOnlySequence<byte> PayloadAsReadOnlySequence()
        {
            Segment? first = null, last = null;
            for (var offset = 0; offset < chunk.Length; offset += chunkSize)
            {
                var len = Math.Min(chunkSize, chunk.Length - offset);
                var next = new Segment(new ReadOnlyMemory<byte>(chunk, offset, len), last);
                first ??= next;
                last = next;
            }
            if (first is null || last is null) return default;
            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }

        private sealed class Segment : ReadOnlySequenceSegment<byte>
        {
            public Segment(ReadOnlyMemory<byte> memory, Segment? previous)
            {
                Memory = memory;
                if (previous is not null)
                {
                    RunningIndex = previous.RunningIndex + previous.Memory.Length;
                    previous.Next = this;
                }
            }
        }
    }
}
