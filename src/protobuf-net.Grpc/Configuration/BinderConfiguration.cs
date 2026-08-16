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
        static readonly MarshallerFactory[] s_defaultFactories = [ProtoBufMarshallerFactory.Default, ProtoBufMarshallerFactory.GoogleProtobuf];

        /// <summary>
        /// Use the default MarshallerFactory and ServiceBinder
        /// </summary>
        public static BinderConfiguration Default { get; } = new BinderConfiguration(s_defaultFactories, ServiceBinder.Default);

        private BinderConfiguration(MarshallerFactory[] factories, ServiceBinder binder)
        {
            MarshallerCache = new MarshallerCache(factories);
            Binder = binder;
        }
        /// <summary>
        /// The <see cref="ServiceBinder"/> associated with this configuration.
        /// </summary>
        /// <remarks>
        /// Public so that build-time generated bindings can honour a custom binder. While this was
        /// internal, generated code had no way to reach the configured binder and had to fall back to
        /// <see cref="ServiceBinder.Default"/>, silently ignoring a consumer's override.
        /// </remarks>
        public ServiceBinder Binder { get; private set; }

        internal MarshallerCache MarshallerCache { get; }

        /// <summary>
        /// Create a new binding configuration
        /// </summary>
        public static BinderConfiguration Create(IList<MarshallerFactory>? marshallerFactories = null, ServiceBinder? binder = null)
        {
            if (marshallerFactories == null || marshallerFactories.SequenceEqual(s_defaultFactories))
                marshallerFactories = s_defaultFactories;

            if (binder == null) binder = ServiceBinder.Default;

            if (marshallerFactories == s_defaultFactories && binder == Default.Binder) return Default;
            // note we create a copy of the factories at this point, to prevent further mutation by the caller
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
