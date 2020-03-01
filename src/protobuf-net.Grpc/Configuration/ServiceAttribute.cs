using System;
using System.ComponentModel;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Explicitly indicates that an interface represents a gRPC service
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    [ImmutableObject(true)]
    public sealed class ServiceAttribute : Attribute
    {
        /// <summary>
        /// The name of the service
        /// </summary>
        public string? Name { get; }
        /// <summary>
        /// Create a new instance of the attribute
        /// </summary>
        public ServiceAttribute(string? name = null)
            => Name = name;
    }
}
