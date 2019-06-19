namespace ProtoBuf.Grpc.Configuration
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


        private BinderConfiguration(MarshallerFactory marshallerFactory, ServiceBinder binder)
        {
            MarshallerFactory = marshallerFactory;
            Binder = binder;
        }
        internal ServiceBinder Binder { get; private set; }
        internal MarshallerFactory MarshallerFactory { get; }

        /// <summary>
        /// Create a new binding configuration
        /// </summary>
        public static BinderConfiguration Create(MarshallerFactory? marshallerFactory = null, ServiceBinder? binder = null)
        {
            if (marshallerFactory == null) marshallerFactory = MarshallerFactory.Default;
            if (binder == null) binder = ServiceBinder.Default;

            if (marshallerFactory == Default.MarshallerFactory && binder == Default.Binder) return Default;
            return new BinderConfiguration(marshallerFactory, binder);
        }
    }
}
