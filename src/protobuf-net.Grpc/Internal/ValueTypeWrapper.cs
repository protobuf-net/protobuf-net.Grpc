using System;
using System.Linq;
using System.Reflection;
using Grpc.Core;
using ProtoBuf.Grpc.Configuration;

namespace ProtoBuf.Grpc.Internal
{
    public class ValueTypeWrapper<T> where T : struct
    {
        public T Value;

        public ValueTypeWrapper(T value)
        {
            Value = value;
        }
    }

    internal class ValueTypeWrapperMarshallerFactory : MarshallerFactory
    {
        private MarshallerCache _cache;
        static readonly MethodInfo s_getMarshaller = typeof(MarshallerCache).GetMethod(
            nameof(MarshallerCache.GetMarshaller), BindingFlags.Instance | BindingFlags.NonPublic)!;
        static readonly MethodInfo s_invokeDeserializer = typeof(ValueTypeWrapperMarshallerFactory).GetMethod(
            nameof(InvokeDeserializer), BindingFlags.Instance | BindingFlags.NonPublic)!;
        static readonly MethodInfo s_invokeSerializer = typeof(ValueTypeWrapperMarshallerFactory).GetMethod(
            nameof(InvokeSerializer), BindingFlags.Instance | BindingFlags.NonPublic)!;

        internal ValueTypeWrapperMarshallerFactory(MarshallerCache cache)
        {
            _cache = cache;
        }

        protected internal override bool CanSerialize(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTypeWrapper<>))
                type = type.GetGenericArguments()[0];

            return _cache.CanSerializeType(type);
        }

        protected internal override Marshaller<T> CreateMarshaller<T>()
        {
            var type = typeof(T);
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(ValueTypeWrapper<>))
            {
                return _cache.GetMarshaller<T>();
            }

            type = type.GetGenericArguments()[0];
            var innerMarshaller = s_getMarshaller.MakeGenericMethod(type).Invoke(_cache, null);
            var serializer = s_invokeSerializer.MakeGenericMethod(type);
            var deserializer = s_invokeDeserializer.MakeGenericMethod(type);
            return new Marshaller<T>((value, context) => serializer.Invoke(this, new object?[] { innerMarshaller, value, context }),
                (context) => (T)deserializer.Invoke(this, new object?[] { innerMarshaller, context })!);
        }

        private void InvokeSerializer<T>(object marshaller, ValueTypeWrapper<T> value, global::Grpc.Core.SerializationContext context) where T : struct
        {
            ((Marshaller<T>)marshaller).ContextualSerializer(value.Value, context);
        }

        private ValueTypeWrapper<T> InvokeDeserializer<T>(object marshaller, DeserializationContext context) where T : struct
        {
            return new ValueTypeWrapper<T>(((Marshaller<T>)marshaller).ContextualDeserializer(context));
        }
    }
}
