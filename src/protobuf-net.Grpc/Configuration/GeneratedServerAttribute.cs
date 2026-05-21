using System;
using System.Diagnostics.CodeAnalysis;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Indicates that a service contract interface has a build-time-generated server-side
    /// bindings class. The source generator emits this attribute on a partial declaration of
    /// the interface, pointing at a generated static class with a <c>Bind&lt;TService&gt;</c> method
    /// the runtime uses instead of <see cref="System.Linq.Expressions"/>-based dispatch.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class GeneratedServerAttribute : Attribute
    {
        /// <summary>
        /// The static class holding generated server-bindings.
        /// </summary>
#if NET8_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
#endif
        public Type Type { get; }

        /// <summary>
        /// Create a new instance pointing at the generated bindings class.
        /// </summary>
        public GeneratedServerAttribute(
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
#endif
            Type type) => Type = type;
    }
}
