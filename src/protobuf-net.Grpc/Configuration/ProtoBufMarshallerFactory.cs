using Grpc.Core;
using ProtoBuf.Meta;
using System;
using System.IO;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Provides protobuf-net implementation of a per-type marshaller
    /// </summary>
    public class ProtoBufMarshallerFactory : MarshallerFactory
    {
        private readonly RuntimeTypeModel _model;
        public ProtoBufMarshallerFactory(RuntimeTypeModel model)
        {
            _model = model;
        }

        protected override Marshaller<T> CreateMarshaller<T>()
            => new Marshaller<T>(Serialize<T>, Deserialize<T>);

        protected override bool CanSerialize(Type type)
            => _model.CanSerialize(type);

        private T Deserialize<T>(byte[] payload)
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

        private byte[] Serialize<T>(T value)
        {
            using (var ms = new MemoryStream())
            {
                _model.Serialize(ms, value, context: null);
                return ms.ToArray();
            }
        }
    }
}
