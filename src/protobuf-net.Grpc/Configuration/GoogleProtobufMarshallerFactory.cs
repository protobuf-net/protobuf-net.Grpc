using Grpc.Core;
using System;
using System.Buffers;
using System.Collections.Concurrent;
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

                    var parserInstance = parser.GetValue(null);
                    if (parserInstance is null) return null;

                    if (iBufferMessage is not null)
                    {
                        // deserializer: parser.ParseFrom(context.PayloadAsReadOnlySequence())
                        var parseFrom = parser.PropertyType.GetMethod("ParseFrom", [typeof(ReadOnlySequence<byte>)])!;
                        var parseFromDel = (Func<ReadOnlySequence<byte>, T>)Delegate.CreateDelegate(
                            typeof(Func<ReadOnlySequence<byte>, T>), parserInstance, parseFrom);
                        deserializer = ctx => parseFromDel(ctx.PayloadAsReadOnlySequence());

                        // serializer composes three calls:
                        //   context.SetPayloadLength(message.CalculateSize());
                        //   MessageExtensions.WriteTo(message, context.GetBufferWriter());
                        //   context.Complete();
                        var calculateSize = iMessage.GetMethod("CalculateSize", Type.EmptyTypes)!;
                        var writeTo = me.GetMethod("WriteTo", [iMessage, typeof(IBufferWriter<byte>)])!;
                        // open instance delegate over IMessage.CalculateSize() — first param is the receiver;
                        // T : IMessage so Func<T, int> is delegate-compatible (contravariant in arg type)
                        var calculateSizeDel = (Func<T, int>)Delegate.CreateDelegate(typeof(Func<T, int>), calculateSize);
                        // static MessageExtensions.WriteTo(IMessage, IBufferWriter<byte>) — Action<T, IBufferWriter<byte>> is contravariant
                        var writeToDel = (Action<T, IBufferWriter<byte>>)Delegate.CreateDelegate(
                            typeof(Action<T, IBufferWriter<byte>>), writeTo);

                        serializer = (message, ctx) =>
                        {
                            ctx.SetPayloadLength(calculateSizeDel(message));
                            writeToDel(message, ctx.GetBufferWriter());
                            ctx.Complete();
                        };
                    }
                    else
                    {
                        // deserializer: parser.ParseFrom(context.PayloadAsNewBuffer())
                        var parseFrom = parser.PropertyType.GetMethod("ParseFrom", [typeof(byte[])])!;
                        var parseFromDel = (Func<byte[], T>)Delegate.CreateDelegate(
                            typeof(Func<byte[], T>), parserInstance, parseFrom);
                        deserializer = ctx => parseFromDel(ctx.PayloadAsNewBuffer());

                        // serializer: context.Complete(MessageExtensions.ToByteArray(message))
                        var toByteArray = me.GetMethod("ToByteArray", [iMessage])!;
                        var toByteArrayDel = (Func<T, byte[]>)Delegate.CreateDelegate(
                            typeof(Func<T, byte[]>), toByteArray);

                        serializer = (message, ctx) => ctx.Complete(toByteArrayDel(message));
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
