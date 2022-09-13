using System;
using System.ComponentModel;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Indicates that this interface can be inherited by a gRPC service.
    /// All methods of this interface will be routed based on the top-level service name.
    /// </summary>
    /// <remarks>This is particularly useful for genenric service APIs.</remarks>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    [ImmutableObject(true)]
    public sealed class SubServiceAttribute : Attribute
    {
    }
}
