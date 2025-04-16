using Grpc.Core;
using ProtoBuf;
using ProtoBuf.Grpc.Configuration;
using ProtoBuf.Serializers;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;
namespace Shared_CS
{
    [ServiceContract(Name = "Hyper.Calculator")]
    public interface ICalculator
    {
        ValueTask<MultiplyResult> MultiplyAsync(MultiplyRequest request);
    }

    [DataContract]
    public class MultiplyRequest
    {
        [DataMember(Order = 1)]
        public int X { get; set; }

        [DataMember(Order = 2)]
        public int Y { get; set; }
    }

    [DataContract]
    public class MultiplyResult
    {
        [DataMember(Order = 1)]
        public int Result { get; set; }
    }

    [ServiceContract]
    public interface IBufferScenarios
    {
        IAsyncEnumerable<SimpleBuffer> Simple(IAsyncEnumerable<SimpleBuffer> source);
        IAsyncEnumerable<AdvancedBuffer> Advanced(IAsyncEnumerable<AdvancedBuffer> source);
    }

    [ProtoContract]
    public sealed class SimpleBuffer
    {
        [ProtoMember(1)]
        public byte[] Data { get; set; } = [];
    }

    [ProtoContract(Serializer = typeof(AdvancedBufferSerializer))]
    public sealed class AdvancedBuffer : IDisposable
    {
        private byte[]? oversized;
        private int count;
        public AdvancedBuffer(int size) : this(ArrayPool<byte>.Shared.Rent(size), size) { }

        public AdvancedBuffer(byte[] buffer, int count)
        {
            this.oversized = buffer;
            this.count = count;
        }

        public void Dispose()
        {
            count = 0;
            var arr = oversized;
            oversized = null;
            if (arr is not null) ArrayPool<byte>.Shared.Return(arr);
        }

        public bool IsNull => oversized is null;

        public Memory<byte> Memory => new(oversized, 0, count);
        public Span<byte> Span => new(oversized, 0, count);
        public int Length => count;
        public ArraySegment<byte> ArraySegment => oversized is null ? default : new(oversized, 0, count);

        public static void Register(BinderConfiguration? binder = null)
        {
            (binder ?? BinderConfiguration.Default).SetMarshaller(marshaller);
        }

        private static readonly Marshaller<AdvancedBuffer> marshaller = new(Serialize, Deserialize);

        private static AdvancedBuffer Deserialize(Grpc.Core.DeserializationContext ctx)
            => Serializer.Deserialize<AdvancedBuffer>(ctx.PayloadAsReadOnlySequence());

        private static void Serialize(AdvancedBuffer buffer, Grpc.Core.SerializationContext ctx)
        {
            // this bit would normally happen automatically via protobuf-net.Grpc
            using var measured = Serializer.Measure<AdvancedBuffer>(buffer);
            ctx.SetPayloadLength(checked((int)measured.Length));
            measured.Serialize(ctx.GetBufferWriter());
            ctx.Complete();

            // this is the main reason we want a custom marshaller: to recycle (dispose) the buffer
            // after use
            buffer.Dispose();
        }


        private sealed class AdvancedBufferSerializer : ISerializer<AdvancedBuffer>, IMemoryConverter<ArraySegment<byte>, byte>
        {
            SerializerFeatures ISerializer<AdvancedBuffer>.Features => SerializerFeatures.CategoryMessage | SerializerFeatures.WireTypeString;

            Memory<byte> IMemoryConverter<ArraySegment<byte>, byte>.Expand(ISerializationContext context, ref ArraySegment<byte> value, int additionalCapacity)
            {
                if (additionalCapacity == 0) return default;
                int oldCount = value.Count;

                if (value.Array is null || value.Array.Length == 0)
                {
                    // no existing array
                    var arr = ArrayPool<byte>.Shared.Rent(additionalCapacity);
                    value = new(arr, 0, additionalCapacity);
                    return value.AsMemory();
                }

                Debug.Assert(value.Offset == 0, "we don't *expect* non-zero offsets here");
                if (value.Offset != 0)
                {
                    // that's definitely not ours! leave the old one alone
                    var oldArray = value.Array;
                    var oversized = ArrayPool<byte>.Shared.Rent(oldCount + additionalCapacity);
                    value.AsSpan().CopyTo(oversized);
                    value = new(oversized, 0, oldCount + additionalCapacity);
                    return new(oversized, oldCount, additionalCapacity);
                }

                // pre-existing
                // since this is during deserialization, we'll *assume* the buffers are ours, and we can use the spare portion as needed
                var space = value.Array.Length - oldCount;
                if (additionalCapacity <= space)
                {
                    value = new ArraySegment<byte>(value.Array, 0, oldCount + additionalCapacity);
                    return new(value.Array, oldCount, additionalCapacity);
                }

                var newBuffer = ArrayPool<byte>.Shared.Rent(value.Count + additionalCapacity);
                value.AsSpan().CopyTo(newBuffer);
                ArrayPool<byte>.Shared.Return(value.Array);
                value = new(newBuffer, 0, oldCount + additionalCapacity);
                return new(newBuffer, oldCount, additionalCapacity);
            }

            int IMemoryConverter<ArraySegment<byte>, byte>.GetLength(in ArraySegment<byte> value) => value.Count;

            Memory<byte> IMemoryConverter<ArraySegment<byte>, byte>.GetMemory(in ArraySegment<byte> value) => value.AsMemory();

            ArraySegment<byte> IMemoryConverter<ArraySegment<byte>, byte>.NonNull(in ArraySegment<byte> value) => value.Array is null ? ArraySegment<byte>.Empty : value;

            AdvancedBuffer ISerializer<AdvancedBuffer>.Read(ref ProtoReader.State state, AdvancedBuffer value)
            {
                int field;
                ArraySegment<byte> buffer = default;
                while ((field = state.ReadFieldHeader()) > 0)
                {
                    switch (field)
                    {
                        case 1:
                            buffer = state.AppendBytes(buffer, this);
                            break;
                        default:
                            state.SkipField();
                            break;
                    }
                }
                return new(buffer.Array!, buffer.Count);
            }

            void ISerializer<AdvancedBuffer>.Write(ref ProtoWriter.State state, AdvancedBuffer value)
            {
                if (!value.IsNull)
                {
                    state.WriteFieldHeader(1, WireType.String);
                    state.WriteBytes(value.ArraySegment);
                }
            }
        }
    }
}
