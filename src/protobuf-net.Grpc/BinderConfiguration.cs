using Grpc.Core;

namespace ProtoBuf.Grpc
{
    /// <summary>
    /// Describes rules for binding to gRPC services
    /// </summary>
    public sealed class BinderConfiguration
    {
        /// <summary>
        /// Use the default MarshallerFactory and ServiceBinder
        /// </summary>
        public static BinderConfiguration Default { get; } = new BinderConfiguration(MarshallerFactory.Default, ServiceBinder.Default);

        private readonly MarshallerFactory _marshallerFactory;

        private BinderConfiguration(MarshallerFactory marshallerFactory, ServiceBinder binder)
        {
            _marshallerFactory = marshallerFactory;
            Binder = binder;
        }
        internal ServiceBinder Binder { get; private set; }

        internal Marshaller<T> GetMarshaller<T>() => _marshallerFactory.GetMarshaller<T>();

        /// <summary>
        /// Create a new binding configuration
        /// </summary>
        public static BinderConfiguration Create(MarshallerFactory? marshallerFactory = null, ServiceBinder? binder = null)
        {
            if (marshallerFactory == null) marshallerFactory = MarshallerFactory.Default;
            if (binder == null) binder = ServiceBinder.Default;

            if (marshallerFactory == Default._marshallerFactory && binder == Default.Binder) return Default;
            return new BinderConfiguration(marshallerFactory, binder);
        }
    }
}
