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
        internal ServiceBinder Binder { get; }
        internal MarshallerCache MarshallerCache { get; }
        internal bool BindAllOperationsToService { get; private set; }

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
        /// Create a new binding configuration
        /// </summary>
        /// <remarks>
        /// This method overloads <see cref="Create" /> with an additional parameter bindAllOperationsToService.
        /// When set to true, this will bind all operations to the service currently being bound, even when defined on a base interface.
        /// This is useful if several services implement a common interface, for example :
        /// <code>
        /// interface IMonitoredService { ServiceReport GetStatus(); }
        /// </code>
        /// In this case, we don't want to call *any* service implementing this, but on the contrary to have
        /// *all* services be queriable individually on /MySpecificService/GetStatus.
        /// </remarks>
        /// <param name="marshallerFactories">a list of <see creef="MarshallerFactory" /> that will be checked in turn to find one that can serialize a given type, or null to use default</param>
        /// <param name="binder">a <see cref="ServiceBinder" /> to use, or null to use the default</param>
        /// <param name="bindAllOperationsToService">true to map all operations to the service being bound (see remark)</param>
        public static BinderConfiguration Create(IList<MarshallerFactory>? marshallerFactories, ServiceBinder? binder, bool bindAllOperationsToService)
        {
            var config = Create(marshallerFactories, binder);
            config.BindAllOperationsToService = bindAllOperationsToService;
            return config;
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
