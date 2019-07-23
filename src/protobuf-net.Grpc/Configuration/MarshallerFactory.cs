using Grpc.Core;
using ProtoBuf.Meta;
using System;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Provides per-type serialization services
    /// </summary>
    public abstract class MarshallerFactory
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        protected MarshallerFactory() {}

        /// <summary>
        /// Deserializes an object from a payload
        /// </summary>
        protected virtual T Deserialize<T>(byte[] payload)
            => throw new NotImplementedException("You must override either CreateMarshaller or both Serialize/Deserialize");

        /// <summary>
        /// Serializes an object to a payload
        /// </summary>
        protected virtual byte[] Serialize<T>(T value)
            => throw new NotImplementedException("You must override either CreateMarshaller or both Serialize/Deserialize");

        /// <summary>
        /// Create a typed marshaller (this value is cached and reused automatically)
        /// </summary>
        protected internal virtual Marshaller<T> CreateMarshaller<T>()
            => new Marshaller<T>(Serialize<T>, Deserialize<T>);

        /// <summary>
        /// Indicates whether a type should be considered as a serializable data type
        /// </summary>
        protected internal abstract bool CanSerialize(Type type);
    }
}
