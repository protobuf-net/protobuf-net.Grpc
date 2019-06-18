using Grpc.Core;
using ProtoBuf.Meta;
using System;
using System.ComponentModel;
using System.IO;

namespace ProtoBuf.Grpc.Internal
{
    [Obsolete(Reshape.WarningMessage, false)]
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public static class MarshallerCache<T>
    {
        [Obsolete(Reshape.WarningMessage, false)]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public static Marshaller<T> Instance { get; } = typeof(T) == typeof(Empty)
            ? (Marshaller<T>)(object)Empty.Marshaller : new Marshaller<T>(Serialize, Deserialize);

        private static readonly RuntimeTypeModel _model = RuntimeTypeModel.Default;

        private static T Deserialize(byte[] payload)
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

        private static byte[] Serialize(T value)
        {
            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, value);
                return ms.ToArray();
            }
        }
    }
}
