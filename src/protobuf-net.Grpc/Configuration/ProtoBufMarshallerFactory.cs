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

        private readonly Options _options;
        private readonly TypeModel _model;
        // note: these are the same *object*, but pre-checked for optional API support, for efficiency
        // (the minimum .NET object size means that the extra fields don't cost anything)
        private readonly IMeasuredProtoOutput<IBufferWriter<byte>>? _measuredWriterModel;
        private readonly IProtoInput<ReadOnlySequence<byte>>? _squenceReaderModel;

        /// <summary>
        /// Create a new factory using a specific protobuf-net model
        /// </summary>
        public static MarshallerFactory Create(TypeModel? model = null, Options options = Options.None)
        {
            model ??= RuntimeTypeModel.Default;
            if (options == Options.None && model == RuntimeTypeModel.Default) return Default;
            return new ProtoBufMarshallerFactory(model, options);
        }

        /// <summary>
        /// Create a new factory using a specific protobuf-net model
        /// </summary>
        public static MarshallerFactory Create(RuntimeTypeModel model, Options options)
            => Create((TypeModel)model, options);

        internal ProtoBufMarshallerFactory(TypeModel model, Options options)
        {
            _model = model;
            _options = options;
            // test these once rather than every time
            _measuredWriterModel = model as IMeasuredProtoOutput<IBufferWriter<byte>>;
            _squenceReaderModel = model as IProtoInput<ReadOnlySequence<byte>>;
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
            catch (NotSupportedException) { writer = default; }
            catch (NotImplementedException) { writer = default; }
            return writer is object;
        }
        private void ContextualSerialize<T>(T value, global::Grpc.Core.SerializationContext context)
        {

            if (_measuredWriterModel is object)
            {   // forget what we think we know about TypeModel; if we have protobuf-net 3.*, we can do this

                RecordUplevelBufferWrite();

                using var measured = _measuredWriterModel.Measure(value);
                int len = checked((int)measured.Length);

                // speculative API; see https://github.com/grpc/grpc-dotnet/pull/611 and
                // https://github.com/grpc/grpc/pull/20691
                //TODO: context.SetPayloadLength(len);

                if (TryGetBufferWriter(context, out var writer))
                {   // write to the buffer-writer API
                    _measuredWriterModel.Serialize(measured, writer!);
                    context.Complete();
                }
                else
                {
                    // the buffer-writer API wasn't supported, but we can still optimize by right-sizing
                    // a MemoryStream to write to, to avoid a resize etc
                    context.Complete(Serialize<T>(value, len));
                }
            }
            else
            {
                context.Complete(Serialize<T>(value));
            }
        }

        private T ContextualDeserialize<T>(global::Grpc.Core.DeserializationContext context)
        {
            var ros = context.PayloadAsReadOnlySequence();
            if (_squenceReaderModel is object)
            {   // forget what we think we know about TypeModel; if we have protobuf-net 3.*, we can do this
                RecordUplevelBufferRead();
                return _squenceReaderModel.Deserialize<T>(ros);
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

        private byte[] Serialize<T>(T value, int length)
        {

            if (length == 0) return Array.Empty<byte>();

            var arr = new byte[length];
            using var ms = new MemoryStream(arr);
            _model.Serialize(ms, value, context: null);
            if (ms.Length != length) throw new InvalidOperationException(
                $"Length miscalculated; expected {length}, got {ms.Length}");
            return arr;
        }
    }
}
