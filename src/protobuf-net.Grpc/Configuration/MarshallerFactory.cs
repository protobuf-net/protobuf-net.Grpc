using Grpc.Core;
using ProtoBuf.Grpc.Internal;
using ProtoBuf.Meta;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Provides per-type serialization services
    /// </summary>
    public abstract class MarshallerFactory
    {
        /// <summary>
        /// Uses the default protobuf-net serializer
        /// </summary>
        public static MarshallerFactory Default { get; } = new ProtoBufMarshallerFactory(RuntimeTypeModel.Default);

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
        protected virtual Marshaller<T> CreateMarshaller<T>()
            => new Marshaller<T>(Serialize<T>, Deserialize<T>);

        /// <summary>
        /// Indicates whether a type should be considered as a serializable data type
        /// </summary>
        protected abstract bool CanSerialize(Type type);

        private readonly ConcurrentDictionary<Type, object> _marshallers = new ConcurrentDictionary<Type, object>
        {
#pragma warning disable CS0618 // Empty
            [typeof(Empty)] = Empty.Marshaller
#pragma warning restore CS0618
        };

        internal Marshaller<T> GetMarshaller<T>()
            => _marshallers.TryGetValue(typeof(T), out var obj) ? (Marshaller<T>)obj : CreateAndAdd<T>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private Marshaller<T> CreateAndAdd<T>()
        {
            var obj = CreateMarshaller<T>();
            if (!_marshallers.TryAdd(typeof(T), obj)) obj = (Marshaller<T>)_marshallers[typeof(T)];
            return obj;
        }
    }
}
