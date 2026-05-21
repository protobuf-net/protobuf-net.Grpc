using System;
using System.Diagnostics.CodeAnalysis;

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
#if NET8_0_OR_GREATER
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods
            | DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
        public Type Type { get; }
        /// <summary>
        /// Create a new ProxyAttribute instance
        /// </summary>
        /// <param name="type">Indicates the proxy type</param>
        public ProxyAttribute(
#if NET8_0_OR_GREATER
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicMethods
                | DynamicallyAccessedMemberTypes.PublicConstructors
                | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
            Type type) => Type = type;
    }
}
