using System;
using System.Diagnostics.CodeAnalysis;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Previously stamped by the source generator on a partial interface declaration to wire up
    /// server-side bindings. Replaced by <c>GeneratedProxyRegistry</c> + a generated
    /// <c>[ModuleInitializer]</c>, which is fully static and needs no attribute lookup at bind time.
    /// Kept for source compatibility with the <c>1.2.10</c> pre-release; runtime ignores it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    [Obsolete("No longer required: server bindings are now registered via the GeneratedProxyRegistry at module-init time. This attribute is a no-op and will be removed in a future major version.")]
    public sealed class GeneratedServerAttribute : Attribute
    {
        /// <summary>
        /// The static class that previously held generated server-bindings (now ignored at runtime).
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
