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
        static readonly MarshallerFactory[] s_defaultFactories = new MarshallerFactory[] { ProtoBufMarshallerFactory.Default };

        /// <summary>
        /// Use the default MarshallerFactory and ServiceBinder
        /// </summary>
        public static BinderConfiguration Default { get; } = new BinderConfiguration(s_defaultFactories, ServiceBinder.Default);

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
            if (marshallerFactories == null || marshallerFactories.SequenceEqual(s_defaultFactories))
                marshallerFactories = s_defaultFactories;

            if (binder == null) binder = ServiceBinder.Default;

            if (marshallerFactories == s_defaultFactories && binder == Default.Binder) return Default;
            // note we create a copy of the factories at this point, to prevent further mutation by the caller
            return new BinderConfiguration(marshallerFactories.ToArray(), binder);
        }
    }
}
