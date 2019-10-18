using ProtoBuf.Meta;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Provides protobuf-net implementation of a per-type marshaller
    /// </summary>
    public partial class ProtoBufMarshallerFactory : MarshallerFactory
    {
        /// <summary>
        /// Options that control protobuf-net marshalling
        /// </summary>
        [Flags]
        public enum Options
        {
            /// <summary>
            /// No options
            /// </summary>
            None = 0,
            /// <summary>
            /// Enforce that only contract-types should be allowed
            /// </summary>
            ContractTypesOnly = 1,
        }

        /// <summary>
        /// Uses the default protobuf-net serializer
        /// </summary>
        public static MarshallerFactory Default { get; } = new ProtoBufMarshallerFactory(RuntimeTypeModel.Default, Options.None);

        private readonly RuntimeTypeModel _model;
        private readonly Options _options;

        /// <summary>
        /// Create a new factory using a specific protobuf-net model
        /// </summary>
        public static MarshallerFactory Create(RuntimeTypeModel? model = null, Options options = Options.None)
        {
            if (model == null) model = RuntimeTypeModel.Default;
            if (options == Options.None && model == RuntimeTypeModel.Default) return Default;
            return new ProtoBufMarshallerFactory(model, options);
        }

        internal ProtoBufMarshallerFactory(RuntimeTypeModel model, Options options)
        {
            _model = model;
            _options = options;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Has(Options option) => (_options & option) == option;

        /// <summary>
        /// Deserializes an object from a payload
        /// </summary>
        protected internal override global::Grpc.Core.Marshaller<T> CreateMarshaller<T>()
           => new global::Grpc.Core.Marshaller<T>(ContextualSerialize<T>, ContextualDeserialize<T>);

#if DEBUG
        private static int _uplevelBufferReadCount, _uplevelBufferWriteCount;
        public static int UplevelBufferReadCount => Volatile.Read(ref _uplevelBufferReadCount);
        public static int UplevelBufferWriteCount => Volatile.Read(ref _uplevelBufferWriteCount);

        static partial void RecordUplevelBufferRead() => Interlocked.Increment(ref _uplevelBufferReadCount);
        static partial void RecordUplevelBufferWrite() => Interlocked.Increment(ref _uplevelBufferWriteCount);
#endif

        static partial void RecordUplevelBufferRead();
        static partial void RecordUplevelBufferWrite();

        private bool TryGetBufferWriter(global::Grpc.Core.SerializationContext context, out IBufferWriter<byte>? writer)
        {
            // the managed implementation does not yet implement this API
            try { writer = context.GetBufferWriter(); }
            catch { writer = default; }
            return writer is object;
        }
        private void ContextualSerialize<T>(T value, global::Grpc.Core.SerializationContext context)
        {
            if ((object)_model is IProtoOutput<IBufferWriter<byte>> native
                && TryGetBufferWriter(context, out var writer))
            {   // forget what we think we know about TypeModel; if we have protobuf-net 3.*, we can do this
                RecordUplevelBufferWrite();
                native.Serialize<T>(writer!, value);
                context.Complete();
            }
            else
            {
                context.Complete(Serialize<T>(value));
            }
        }

        private T ContextualDeserialize<T>(global::Grpc.Core.DeserializationContext context)
        {
            var ros = context.PayloadAsReadOnlySequence();
            if ((object)_model is IProtoInput<ReadOnlySequence<byte>> native)
            {   // forget what we think we know about TypeModel; if we have protobuf-net 3.*, we can do this
                RecordUplevelBufferRead();
                return native.Deserialize<T>(ros);
            }

            // 2.4.2+ can use array-segments
            IProtoInput<ArraySegment<byte>> segmentReader = _model;

            // can we go direct to a single segment?
            if (ros.IsSingleSegment && MemoryMarshal.TryGetArray(ros.First, out var segment))
            {
                return segmentReader.Deserialize<T>(segment);
            }

            // otherwise; linearize the data
            var oversized = ArrayPool<byte>.Shared.Rent(context.PayloadLength);
            try
            {
                ros.CopyTo(oversized);
                return segmentReader.Deserialize<T>(new ArraySegment<byte>(oversized, 0, context.PayloadLength));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(oversized);
            }
        }

        /// <summary>
        /// Indicates whether a type should be considered as a serializable data type
        /// </summary>
        protected internal override bool CanSerialize(Type type)
            => Has(Options.ContractTypesOnly)
                ? _model.CanSerializeContractType(type)
                : _model.CanSerialize(type);

        /// <summary>
        /// Deserializes an object from a payload
        /// </summary>
        protected override T Deserialize<T>(byte[] payload)
        {
            IProtoInput<byte[]> input = _model;
            return input.Deserialize<T>(payload);
        }

        private T Deserialize<T>(byte[] payload, int offset, int count)
        {
            IProtoInput<ArraySegment<byte>> input = _model;
            return input.Deserialize<T>(new ArraySegment<byte>(payload, offset, count));
        }

        /// <summary>
        /// Serializes an object to a payload
        /// </summary>
        protected override byte[] Serialize<T>(T value)
        {
            using var ms = new MemoryStream();
            _model.Serialize(ms, value, context: null);
            return ms.ToArray();
        }
    }
}
