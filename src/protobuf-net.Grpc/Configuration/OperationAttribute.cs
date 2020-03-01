using System;
using System.ComponentModel;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Explicitly indicates that a metho represents a gRPC method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    [ImmutableObject(true)]
    public sealed class OperationAttribute : Attribute
    {
        /// <summary>
        /// The name of the operation
        /// </summary>
        public string? Name { get; }
        /// <summary>
        /// Create a new instance of the attribute
        /// </summary>
        public OperationAttribute(string? name = null)
            => Name = name;
    }
}
