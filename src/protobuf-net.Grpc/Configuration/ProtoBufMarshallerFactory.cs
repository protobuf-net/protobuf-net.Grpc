using ProtoBuf.Meta;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Provides protobuf-net implementation of a per-type marshaller
    /// </summary>
    public class ProtoBufMarshallerFactory : MarshallerFactory
    {
        [Flags]
        public enum Options
        {
            None = 0,
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
#if PLAT_NOSPAN
            using (var ms = new MemoryStream(payload))
            using (var reader = ProtoReader.Create(ms, _model))
            {
                return (T)_model.Deserialize(reader, null, typeof(T));
            }
#else
            using (var reader = ProtoReader.Create(out var state, payload, _model))
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
            using (var ms = new MemoryStream())
            {
                _model.Serialize(ms, value, context: null);
                return ms.ToArray();
            }
        }
    }
}
