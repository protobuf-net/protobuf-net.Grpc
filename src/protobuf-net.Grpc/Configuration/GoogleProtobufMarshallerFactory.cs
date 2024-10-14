using Grpc.Core;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace ProtoBuf.Grpc.Configuration
{
    internal sealed class GoogleProtobufMarshallerFactory : MarshallerFactory
    {
        internal static MarshallerFactory Default { get; } = new GoogleProtobufMarshallerFactory();

        private GoogleProtobufMarshallerFactory() { }

        protected internal override bool CanSerialize(Type type)
        {
            if (_knownTypes.TryGetValue(type, out var existing))
            {
                return existing is not null;
            }
            var created = s_Create.MakeGenericMethod(type).Invoke(null, null);
            _knownTypes[type] = created;
            return created is not null;
        }
        static readonly MethodInfo s_Create = typeof(GoogleProtobufMarshallerFactory).GetMethod(nameof(AutoDetectProtobufMarshaller), BindingFlags.Static | BindingFlags.NonPublic)!;

        static readonly ConcurrentDictionary<Type, object?> s_KnownTypes = new();
        protected internal override Marshaller<T> CreateMarshaller<T>()
        {
            if (_knownTypes.TryGetValue(typeof(T), out var existing))
            {
                return (Marshaller<T>)existing!;
            }
            var created = AutoDetectProtobufMarshaller<T>();
            _knownTypes[typeof(T)] = created;
            return created!;
        }

        private static readonly ConcurrentDictionary<Type, object?> _knownTypes = new ConcurrentDictionary<Type, object?>();

        // attempt to auto-detect the patterns exposed by Google.Protobuf types;
        // this is (by necessity) reflection-based and imperfect
        static Marshaller<T>? AutoDetectProtobufMarshaller<T>()
        {
            try
            {
                if (typeof(T).GetProperty("Parser", BindingFlags.Public | BindingFlags.Static) is { } parser
                    && FindIMessage(out var iBufferMessage) is { } iMessage
                    && iMessage.Assembly.GetType("Google.Protobuf.MessageExtensions") is { } me)
                {
                    Func<DeserializationContext, T> deserializer;
                    Action<T, global::Grpc.Core.SerializationContext> serializer;

                    if (iBufferMessage is not null)
                    {
                        /* we want to generate:
// write
context.SetPayloadLength(message.CalculateSize());
global::Google.Protobuf.MessageExtensions.WriteTo(message, context.GetBufferWriter());
context.Complete();

// read
parser.ParseFrom(context.PayloadAsReadOnlySequence()
*/
                        var context = Expression.Parameter(typeof(global::Grpc.Core.DeserializationContext), "context");
                        var parseFrom = parser.PropertyType.GetMethod("ParseFrom", [typeof(ReadOnlySequence<byte>)])!;
                        Expression body = Expression.Call(Expression.Constant(parser.GetValue(null), parser.PropertyType),
                            parseFrom, Expression.Call(context, nameof(DeserializationContext.PayloadAsReadOnlySequence), Type.EmptyTypes));
                        deserializer = Expression.Lambda<Func<DeserializationContext, T>>(body, context).Compile();

                        var message = Expression.Parameter(typeof(T), "message");
                        context = Expression.Parameter(typeof(global::Grpc.Core.SerializationContext), "context");
                        var setPayloadLength = typeof(global::Grpc.Core.SerializationContext).GetMethod(nameof(global::Grpc.Core.SerializationContext.SetPayloadLength), [typeof(int)])!;
                        var calculateSize = iMessage.GetMethod("CalculateSize", Type.EmptyTypes)!;
                        var writeTo = me.GetMethod("WriteTo", [iMessage, typeof(IBufferWriter<byte>)])!;
                        body = Expression.Block(
                            Expression.Call(context, setPayloadLength, Expression.Call(message, calculateSize)),
                            Expression.Call(writeTo, message, Expression.Call(context, "GetBufferWriter", Type.EmptyTypes)),
                            Expression.Call(context, "Complete", Type.EmptyTypes)
                        );
                        serializer = Expression.Lambda<Action<T, global::Grpc.Core.SerializationContext>>(body, message, context).Compile();
                    }
                    else
                    {
                        /* we want to generate:
// write
context.Complete(global::Google.Protobuf.MessageExtensions.ToByteArray(message));

// read
parser.ParseFrom(context.PayloadAsNewBuffer());
*/

                        var context = Expression.Parameter(typeof(global::Grpc.Core.DeserializationContext), "context");
                        var parseFrom = parser.PropertyType.GetMethod("ParseFrom", [typeof(byte[])])!;
                        Expression body = Expression.Call(Expression.Constant(parser.GetValue(null), parser.PropertyType),
                            parseFrom, Expression.Call(context, nameof(DeserializationContext.PayloadAsNewBuffer), Type.EmptyTypes));
                        deserializer = Expression.Lambda<Func<DeserializationContext, T>>(body, context).Compile();

                        var message = Expression.Parameter(typeof(T), "message");
                        context = Expression.Parameter(typeof(global::Grpc.Core.SerializationContext), "context");
                        var toByteArray = me.GetMethod("ToByteArray", [iMessage])!;
                        var complete = typeof(global::Grpc.Core.SerializationContext).GetMethod(
                            nameof(global::Grpc.Core.SerializationContext.Complete), [typeof(byte[])])!;
                        body = Expression.Call(context, complete, Expression.Call(toByteArray, message));
                        serializer = Expression.Lambda<Action<T, global::Grpc.Core.SerializationContext>>(body, message, context).Compile();
                    }
                    return new Marshaller<T>(serializer, deserializer);
                }
            }
            catch { } // this is very much a best-efforts thing
            return null;

            static Type? FindIMessage(out Type? iBufferMessage)
            {
                Type? iMessage = null;
                iBufferMessage = null;
                foreach (var it in typeof(T).GetInterfaces())
                {
                    if (it.Name == "IBufferMessage" && it.Namespace == "Google.Protobuf" && !it.IsGenericType)
                    {
                        iBufferMessage = it;
                    }
                    else if (it.Name == "IMessage" && it.Namespace == "Google.Protobuf" && !it.IsGenericType)
                    {
                        iMessage = it;
                    }
                }
                return iMessage;
            }
        }
    }
}
