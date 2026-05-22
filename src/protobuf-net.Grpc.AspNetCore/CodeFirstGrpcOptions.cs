namespace ProtoBuf.Grpc.Server
{
    /// <summary>
    /// Options for the code-first gRPC service provider.
    /// </summary>
    public sealed class CodeFirstGrpcOptions
    {
        /// <summary>
        /// <para>
        /// If <see langword="true"/>, per-method bind failures (e.g. a missing marshaller for a
        /// request / response type) are logged as warnings and the remaining methods on the
        /// contract continue to bind. The host comes up with a partial service surface.
        /// </para>
        /// <para>
        /// If <see langword="false"/> (default), a per-method bind failure throws at startup so
        /// the host fails fast and the original exception is visible in the startup stack trace.
        /// This is the recommended setting for production services — a partial bind tends to
        /// surface as opaque "unimplemented" errors at request time, which are much harder to
        /// diagnose than a loud startup failure.
        /// </para>
        /// <para>
        /// Configure via <c>services.Configure&lt;CodeFirstGrpcOptions&gt;(o =&gt; o.ContinueOnBindFailure = true)</c>.
        /// </para>
        /// </summary>
        public bool ContinueOnBindFailure { get; set; }
    }
}
