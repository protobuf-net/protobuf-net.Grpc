namespace ProtoBuf.Grpc.Internal
{
    // example usage:
    // [Experimental(Experiments.BuildTimeProxies, UrlFormat = Experiments.UrlFormat)]
    // where the id has a corresponding /docs/exp/{id}.md page telling people how to opt in
    internal static class Experiments
    {
        // note: {0} is substituted with the DiagnosticId by the compiler, e.g. .../exp/PBN9001
        //
        // github.io for now: protobuf-net's docs have moved to docs.protobuf-net.dev and this repo
        // is expected to follow before release, at which point only this constant changes.
        //
        // the page name must match the id's *case* - GitHub Pages paths are case-sensitive, so
        // docs/exp/PBN9001.md, not docs/exp/pbn9001.md
        public const string UrlFormat = "https://protobuf-net.github.io/protobuf-net.Grpc/exp/{0}";

        /// <summary>
        /// Compile-time gRPC proxies and server bindings, via <c>[ProtoGrpc]</c>.
        /// </summary>
        /// <remarks>
        /// Deliberately the same id protobuf-net uses for <c>[ProtoModel]</c>: the two halves are
        /// opted into together in practice, and one <c>NoWarn</c> covering both is a feature rather
        /// than an accident. Do not reuse it for anything unrelated.
        /// </remarks>
        public const string BuildTimeProxies = "PBN9001";
    }
}
