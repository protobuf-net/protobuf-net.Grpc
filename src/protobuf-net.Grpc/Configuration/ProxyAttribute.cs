using System;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Specifies a pre-generated proxy that provides an implementation of this service
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class ProxyAttribute : Attribute
    {
        /// <summary>
        /// Indicates the proxy type
        /// </summary>
        public Type Type { get; }
        /// <summary>
        /// Create a new ProxyAttribute instance
        /// </summary>
        /// <param name="type">Indicates the proxy type</param>
        public ProxyAttribute(Type type) => Type = type;
    }
}