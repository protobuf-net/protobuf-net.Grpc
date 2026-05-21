using Microsoft.CodeAnalysis;

namespace ProtoBuf.Grpc.BuildTools;

internal static class Diagnostics
{
    private const string Category = "ProtoBuf.Grpc";

    // PBNG001 is intentionally retired: the generator no longer requires the user's interface to be
    // declared partial — everything lives in ProtoBuf.Grpc.Generated.* and registration happens via
    // [ModuleInitializer].

    public static readonly DiagnosticDescriptor InterfaceMustNotBeNested = new(
        id: "PBNG002",
        title: "Service interface cannot be nested",
        messageFormat: "Interface '{0}' is a service contract but is nested inside another type; the source generator only supports top-level interfaces. Move the interface out of '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedMethodShape = new(
        id: "PBNG003",
        title: "Service method shape is not supported by the source generator",
        messageFormat: "Method '{0}.{1}' has a signature the source generator does not currently emit; the runtime IL-emit fallback will be used, which is not AOT-friendly",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor GenericInterfaceNotSupported = new(
        id: "PBNG004",
        title: "Generic interfaces are not supported",
        messageFormat: "Interface '{0}' is generic; the source generator does not currently emit proxies for generic service contracts",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
