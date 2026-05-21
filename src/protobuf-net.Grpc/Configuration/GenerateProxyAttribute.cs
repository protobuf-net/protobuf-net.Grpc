using System;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Previously an opt-in marker for the protobuf-net.Grpc source generator. The generator now
    /// auto-detects any interface tagged <c>[Service]</c> / <c>[ServiceContract]</c>, so this
    /// attribute is no longer required; left in place for source compatibility with the
    /// <c>1.2.10</c> pre-release that required it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    [Obsolete("No longer required: the source generator now auto-detects [Service]/[ServiceContract] interfaces. This attribute is a no-op and will be removed in a future major version.")]
    public sealed class GenerateProxyAttribute : Attribute
    {
    }
}
