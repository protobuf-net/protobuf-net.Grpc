using Grpc.Core;
using ProtoBuf.Grpc.Internal;
using ProtoBuf.Meta;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace ProtoBuf.Grpc
{
    /// <summary>
    /// Provides per-type serialization services
    /// </summary>
    public class MarshallerFactory
    {
        /// <summary>
        /// Uses the default protobuf-net serializer
        /// </summary>
        public static MarshallerFactory Default { get; } = new MarshallerFactory();

        /// <summary>
        /// Create a new instance
        /// </summary>
        protected MarshallerFactory() {}

        /// <summary>
        /// Create a typed marshaller
        /// </summary>
        protected virtual Marshaller<T> CreateMarshaller<T>() => DefaultMarshaller<T>.Instance;

        /// <summary>
        /// Indicates whether a type should be considered as a serializable data type
        /// </summary>
        protected virtual bool CanSerialize(Type type) => RuntimeTypeModel.Default.CanSerialize(type);

        private readonly ConcurrentDictionary<Type, object> _marshallers = new ConcurrentDictionary<Type, object>
        {
#pragma warning disable CS0618
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
