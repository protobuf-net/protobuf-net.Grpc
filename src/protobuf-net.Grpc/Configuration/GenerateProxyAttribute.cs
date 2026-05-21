using System;

namespace ProtoBuf.Grpc.Configuration
{
    /// <summary>
    /// Opts an interface in to having a client proxy generated at build time by the
    /// protobuf-net.Grpc source generator. The interface must be declared <c>partial</c>;
    /// the generator emits a companion partial declaration that carries <see cref="ProxyAttribute"/>
    /// pointing at the generated proxy type. This lets the proxy live in static code so the
    /// trimmer and AOT compiler can see (and shake) it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class GenerateProxyAttribute : Attribute
    {
    }
}
