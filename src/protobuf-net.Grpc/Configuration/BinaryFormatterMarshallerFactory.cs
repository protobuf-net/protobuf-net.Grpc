using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Uses BinaryFormatter for serialization
    /// </summary>
    [Obsolete("Use of BinaryFormatter is *extremely* discoraged for security, cross-platform compatibility, compatibility-between-versions, and performance reasons")]
    public sealed class BinaryFormatterMarshallerFactory : MarshallerFactory
    {
        private BinaryFormatterMarshallerFactory() { }
        /// <summary>
        /// Uses BinaryFormatter for serialization
        /// </summary>
        public static MarshallerFactory Default { get; } = new BinaryFormatterMarshallerFactory();

        /// <inheritdoc/>
        protected internal override bool CanSerialize(Type type)
            => type.IsSerializable;

        /// <inheritdoc/>
        protected override T Deserialize<T>(byte[] payload)
        {
            using var ms = new MemoryStream(payload);
            return (T)new BinaryFormatter().Deserialize(ms);
        }

        /// <inheritdoc/>
        protected override byte[] Serialize<T>(T value)
        {
            using var ms = new MemoryStream();
            new BinaryFormatter().Serialize(ms, value);
            return ms.ToArray();
        }


    }
}
