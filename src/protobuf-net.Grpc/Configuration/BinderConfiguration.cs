using Grpc.Core;
using ProtoBuf.Grpc.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Describes rules for binding to gRPC services
    /// </summary>
    public sealed class BinderConfiguration
    {
        // this *must* stay above Default - .cctor order is file order
        readonly MarshallerFactory[] s_defaultFactories = new MarshallerFactory[] { new ProtoBufMarshallerFactory() };

        /// <summary>
        /// Use the default MarshallerFactory and ServiceBinder
        /// </summary>
        public BinderConfiguration()
            :this(new MarshallerFactory[] { new ProtoBufMarshallerFactory() }, new ServiceBinder())
        {
        }

        private BinderConfiguration(MarshallerFactory[] factories, ServiceBinder binder)
        {
            MarshallerCache = new MarshallerCache(factories);
            Binder = binder;
        }
        internal ServiceBinder Binder { get; private set; }
        internal MarshallerCache MarshallerCache { get; }

        /// <summary>
        /// Create a new binding configuration
        /// </summary>
        public static BinderConfiguration Create(IList<MarshallerFactory>? marshallerFactories = null, ServiceBinder? binder = null)
        {
            MarshallerFactory[] s_defaultFactories = new MarshallerFactory[] { new ProtoBufMarshallerFactory() };

            if (marshallerFactories == null || marshallerFactories.SequenceEqual(s_defaultFactories))
                marshallerFactories = s_defaultFactories;

            if (binder == null) binder = new ServiceBinder();

            return new BinderConfiguration(marshallerFactories.ToArray(), binder);
        }

        /// <summary>
        /// Gets a typed marshaller associated with this configuration
        /// </summary>
        public Marshaller<T> GetMarshaller<T>() => MarshallerCache.GetMarshaller<T>();


        /// <summary>
        /// Sets (or resets) a typed marshalled against this configuration
        /// </summary>
        /// <param name="marshaller">The marshaller to use - if null, the cache is reset for this type</param>
        public void SetMarshaller<T>(Marshaller<T>? marshaller) => MarshallerCache.SetMarshaller<T>(marshaller);

        internal MarshallerFactory? TryGetFactory(Type type) => MarshallerCache.TryGetFactory(type);
    }
}
