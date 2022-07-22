using Grpc.Core;
using ProtoBuf.Grpc.Configuration;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.ServiceModel.Channels;

namespace ProtoBuf.Grpc.Internal
{
    internal sealed class MarshallerCache
    {
        private readonly MarshallerFactory[] _factories;
        public MarshallerCache(MarshallerFactory[] factories)
            => _factories = factories ?? throw new ArgumentNullException(nameof(factories));
        internal bool CanSerializeType(Type type)
        {
            if (_marshallers.TryGetValue(type, out var obj)) return obj != null;
            return SlowImpl(this, type);

            static bool SlowImpl(MarshallerCache obj, Type type)
                => _createAndAdd.MakeGenericMethod(type).Invoke(obj, Array.Empty<object>()) != null;
        }
        static readonly MethodInfo _createAndAdd = typeof(MarshallerCache).GetMethod(
            nameof(CreateAndAdd), BindingFlags.Instance | BindingFlags.NonPublic)!;

        private readonly ConcurrentDictionary<Type, object?> _marshallers
            = new ConcurrentDictionary<Type, object?>
            {
#pragma warning disable CS0618 // Empty
                [typeof(Empty)] = Empty.Marshaller
#pragma warning restore CS0618
            };

        internal Marshaller<T> GetMarshaller<T>()
        {
            return (_marshallers.TryGetValue(typeof(T), out var obj)
                ? (Marshaller<T>?)obj : CreateAndAdd<T>()) ?? Throw();

            static Marshaller<T> Throw() => throw new InvalidOperationException("No marshaller available for " + typeof(T).FullName);
        }

        internal void SetMarshaller<T>(Marshaller<T>? marshaller)
        {
            if (marshaller is null)
            {
                _marshallers.TryRemove(typeof(T), out _);
            }
            else
            {
                _marshallers[typeof(T)] = marshaller;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private Marshaller<T>? CreateAndAdd<T>()
        {
            object? obj = CreateMarshaller<T>();
            if (!_marshallers.TryAdd(typeof(T), obj)) obj = _marshallers[typeof(T)];
            return obj as Marshaller<T>;
        }
        private Marshaller<T>? CreateMarshaller<T>()
        {
            foreach (var factory in _factories)
            {
                if (factory.CanSerialize(typeof(T)))
                    return factory.CreateMarshaller<T>();
            }
            return AutoDetectProtobufMarshaller();

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

            // attempt to auto-detect the patterns exposed by Google.Protobuf types;
            // this is (by necessity) reflection-based and imperfect
            static Marshaller<T>? AutoDetectProtobufMarshaller()
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
                            var parseFrom = parser.PropertyType.GetMethod("ParseFrom", new Type[] { typeof(ReadOnlySequence<byte>) });
                            Expression body = Expression.Call(Expression.Constant(parser.GetValue(null), parser.PropertyType),
                                parseFrom, Expression.Call(context, nameof(DeserializationContext.PayloadAsReadOnlySequence), Type.EmptyTypes));
                            deserializer = Expression.Lambda<Func<DeserializationContext, T>>(body, context).Compile();

                            var message = Expression.Parameter(typeof(T), "message");
                            context = Expression.Parameter(typeof(global::Grpc.Core.SerializationContext), "context");
                            var setPayloadLength = typeof(global::Grpc.Core.SerializationContext).GetMethod(nameof(global::Grpc.Core.SerializationContext.SetPayloadLength), new Type[] { typeof(int) });
                            var calculateSize = iMessage.GetMethod("CalculateSize", Type.EmptyTypes);
                            var writeTo = me.GetMethod("WriteTo", new Type[] { iMessage, typeof(IBufferWriter<byte>) });
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
                            var parseFrom = parser.PropertyType.GetMethod("ParseFrom", new Type[] { typeof(byte[]) });
                            Expression body = Expression.Call(Expression.Constant(parser.GetValue(null), parser.PropertyType),
                                parseFrom, Expression.Call(context, nameof(DeserializationContext.PayloadAsNewBuffer), Type.EmptyTypes));
                            deserializer = Expression.Lambda<Func<DeserializationContext, T>>(body, context).Compile();

                            var message = Expression.Parameter(typeof(T), "message");
                            context = Expression.Parameter(typeof(global::Grpc.Core.SerializationContext), "context");
                            var toByteArray = me.GetMethod("ToByteArray", new Type[] { iMessage });
                            var complete = typeof(global::Grpc.Core.SerializationContext).GetMethod(
                                nameof(global::Grpc.Core.SerializationContext.Complete), new Type[] { typeof(byte[]) });
                            body = Expression.Call(context, complete, Expression.Call(toByteArray, message));
                            serializer = Expression.Lambda<Action<T, global::Grpc.Core.SerializationContext>>(body, message, context).Compile();
                        }
                        return new Marshaller<T>(serializer, deserializer);
                    }
                }
                catch { } // this is very much a best-efforts thing
                return null;
            }
        }

        internal MarshallerFactory? TryGetFactory(Type type)
        {
            foreach (var factory in _factories)
            {
                if (factory.CanSerialize(type))
                    return factory;
            }
            return null;
        }

        internal TFactory? TryGetFactory<TFactory>()
        where TFactory : MarshallerFactory
        {
            foreach (var factory in _factories)
            {
                if (factory is TFactory typed)
                    return typed;
            }
            return null;
        }
    }
}
