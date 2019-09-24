using ProtoBuf.Meta;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Provides protobuf-net implementation of a per-type marshaller
    /// </summary>
    public class ProtoBufMarshallerFactory : MarshallerFactory
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

        /* see: https://github.com/grpc/grpc/pull/19471 / https://github.com/grpc/grpc/issues/19470
        /// <summary>
        /// Deserializes an object from a payload
        /// </summary>
        protected internal override global::Grpc.Core.Marshaller<T> CreateMarshaller<T>()
            => new global::Grpc.Core.Marshaller<T>(ContextualSerialize<T>, ContextualDeserialize<T>);
        */

        private void ContextualSerialize<T>(T value, global::Grpc.Core.SerializationContext context)
            => context.Complete(Serialize(value));

        private T ContextualDeserialize<T>(global::Grpc.Core.DeserializationContext context)
        {
            var ros = context.PayloadAsReadOnlySequence();
#if PLAT_PBN_NOSPAN
            // copy the data out of the ROS into a rented buffer, and deserialize
            // from that
            var oversized = ArrayPool<byte>.Shared.Rent(context.PayloadLength);
            try
            {
                ros.CopyTo(oversized);
                return Deserialize<T>(oversized, 0, context.PayloadLength);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(oversized);
            }
#else
            // create a reader directly on the ROS
            using (var reader = ProtoReader.Create(out var state, ros, _model))
            {
                return (T)_model.Deserialize(reader, ref state, null, typeof(T));
            }
#endif
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
            => Deserialize<T>(payload, 0, payload.Length);

        private T Deserialize<T>(byte[] payload, int offset, int count)
        {
#if PLAT_PBN_NOSPAN
            using var ms = new MemoryStream(payload, offset, count);
            using var reader = ProtoReader.Create(ms, _model);
            return (T)_model.Deserialize(reader, null, typeof(T));
#else
            var range = new ReadOnlyMemory<byte>(payload, offset, count);
            using (var reader = ProtoReader.Create(out var state, range, _model))
            {
                return (T)_model.Deserialize(reader, ref state, null, typeof(T));
            }
#endif
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
