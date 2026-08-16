namespace ProtoBuf.Grpc.Internal
{
    // example usage:
    // [Experimental(Experiments.SomeFeature, UrlFormat = Experiments.UrlFormat)]
    // where the id has a corresponding /docs/exp/{id}.md page telling people how to opt in
    internal static class Experiments
    {
        // note: {0} is substituted with the DiagnosticId by the compiler, e.g. .../exp/PBNnnnn
        //
        // github.io for now: protobuf-net's docs have moved to docs.protobuf-net.dev and this repo
        // is expected to follow before release, at which point only this constant changes.
        //
        // the page name must match the id's *case* - GitHub Pages paths are case-sensitive, so
        // docs/exp/PBNnnnn.md, not docs/exp/pbnnnnn.md
        public const string UrlFormat = "https://protobuf-net.github.io/protobuf-net.Grpc/exp/{0}";

        /// <summary>
        /// Compile-time gRPC proxies and server bindings, via <c>[ProtoGrpc]</c>.
        /// </summary>
        /// <remarks>
        /// This is protobuf-net's id, not one of ours, and that is deliberate: build-time proxies
        /// are useless without a compile-time serializer model, so the two are opted into together
        /// and one <c>NoWarn</c> should cover both. Do not reuse it for anything unrelated.
        /// </remarks>
        public const string BuildTimeProxies = "PBN9001";

        /// <summary>
        /// The help link for <see cref="BuildTimeProxies"/>, which points at <em>protobuf-net's</em>
        /// docs rather than ours.
        /// </summary>
        /// <remarks>
        /// The unusual bit, and worth stating so nobody "fixes" it: one experiment gets one page.
        /// Since the id is shared, hosting a second copy here would fork the guidance and guarantee
        /// the two drift. That page links back to this repo's AOT documentation for the gRPC half.
        /// </remarks>
        public const string BuildTimeProxiesUrlFormat = "https://docs.protobuf-net.dev/exp/{0}";
    }
}
