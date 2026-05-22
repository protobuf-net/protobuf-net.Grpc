using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ProtoBuf.Grpc.BuildTools;

[Generator(LanguageNames.CSharp)]
public sealed class ProxySourceGenerator : IIncrementalGenerator
{
    // The two attributes the generator scans for. Either qualifies the interface as a service contract;
    // mirrors the runtime ServiceBinder.IsServiceContract logic.
    private const string ServiceAttributeName = "ProtoBuf.Grpc.Configuration.ServiceAttribute";
    private const string ServiceContractAttributeName = "System.ServiceModel.ServiceContractAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var serviceCandidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ServiceAttributeName,
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, ct) => BuildCandidate(ctx, ct));

        var serviceContractCandidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ServiceContractAttributeName,
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, ct) => BuildCandidate(ctx, ct));

        var combined = serviceCandidates.Collect()
            .Combine(serviceContractCandidates.Collect());

        context.RegisterSourceOutput(combined, static (ctx, pair) =>
        {
            var (a, b) = pair;
            // dedupe by interface full name in case both attributes are present (rare but possible)
            var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            foreach (var candidate in a.Concat(b))
            {
                if (candidate is null) continue;
                if (candidate.Model is not null && !seen.Add(candidate.Model.InterfaceFullName))
                {
                    continue;
                }

                foreach (var diag in candidate.Diagnostics)
                {
                    ctx.ReportDiagnostic(diag);
                }

                if (candidate.Model is { } model)
                {
                    var source = ProxyEmitter.Emit(model);
                    var hintName = SanitizeHintName(model.InterfaceFullName) + ".g.cs";
                    ctx.AddSource(hintName, source);
                }
            }
        });
    }

    private static Candidate? BuildCandidate(GeneratorAttributeSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (ctx.TargetSymbol is not INamedTypeSymbol iface || iface.TypeKind != TypeKind.Interface)
            return null;

        // Skip interfaces that already have an explicit [Proxy(typeof(...))] attribute — the user
        // has their own proxy and we shouldn't second-guess that.
        foreach (var attr in iface.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == "ProtoBuf.Grpc.Configuration.ProxyAttribute")
                return null;
        }

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        if (iface.ContainingType is not null)
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.InterfaceMustNotBeNested,
                iface.Locations.FirstOrDefault(),
                iface.Name,
                iface.ContainingType.ToDisplayString()));
            return new Candidate(null, diagnostics.ToImmutable());
        }

        if (iface.IsGenericType)
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.GenericInterfaceNotSupported,
                iface.Locations.FirstOrDefault(),
                iface.Name));
            return new Candidate(null, diagnostics.ToImmutable());
        }

        var allInterfaces = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        allInterfaces.Add(iface);
        foreach (var i in iface.AllInterfaces) allInterfaces.Add(i);

        // Strategy: emit only when EVERY method on the interface (and bases) is in a shape we can
        // handle. If any method is unsupported, skip — the runtime IL emitter handles a wider set of
        // shapes (IObservable, Stream, etc.) than we currently generate, so we'd regress behavior if
        // we emitted a partial proxy with throwing stubs.
        var ops = ImmutableArray.CreateBuilder<OperationModel>();
        bool hasUnsupportedMethod = false;
        foreach (var i in allInterfaces)
        {
            foreach (var member in i.GetMembers())
            {
                if (member is not IMethodSymbol method) continue;
                if (method.MethodKind != Microsoft.CodeAnalysis.MethodKind.Ordinary) continue;
                if (method.IsStatic) continue;

                if (OperationDiscovery.TryBuild(method, out var op) && op is not null)
                {
                    ops.Add(op);
                }
                else
                {
                    hasUnsupportedMethod = true;
                    diagnostics.Add(Diagnostic.Create(
                        Diagnostics.UnsupportedMethodShape,
                        method.Locations.FirstOrDefault(),
                        i.ToDisplayString(),
                        method.Name));
                }
            }
        }

        // Don't emit if any method is unsupported (would leave the proxy with missing interface
        // implementations) or if no methods are recognised at all (probably a marker interface).
        if (hasUnsupportedMethod || ops.Count == 0)
        {
            return new Candidate(null, diagnostics.ToImmutable());
        }

        var serviceName = GetServiceName(iface);
        var nsName = iface.ContainingNamespace.IsGlobalNamespace ? "" : iface.ContainingNamespace.ToDisplayString();
        var sanitizedBase = MakeSanitizedBase(iface);
        var proxyTypeName = sanitizedBase + "_ClientProxy";
        var proxyFullTypeName = "ProtoBuf.Grpc.Generated." + proxyTypeName;
        var serverBindingsTypeName = sanitizedBase + "_ServerBindings";
        var serverBindingsFullTypeName = "ProtoBuf.Grpc.Generated." + serverBindingsTypeName;
        var initTypeName = sanitizedBase + "_Init";

        var model = new InterfaceModel(
            Namespace: nsName,
            InterfaceName: iface.Name,
            InterfaceFullName: iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            ServiceName: serviceName,
            ProxyTypeName: proxyTypeName,
            ProxyFullTypeName: proxyFullTypeName,
            ServerBindingsTypeName: serverBindingsTypeName,
            ServerBindingsFullTypeName: serverBindingsFullTypeName,
            InitTypeName: initTypeName,
            Operations: ops.ToImmutable());

        return new Candidate(model, diagnostics.ToImmutable());
    }

    private static string GetServiceName(INamedTypeSymbol iface)
    {
        foreach (var attr in iface.GetAttributes())
        {
            var name = attr.AttributeClass?.Name;
            if (name == "ServiceAttribute" || name == "ServiceContractAttribute")
            {
                foreach (var arg in attr.NamedArguments)
                {
                    if (arg.Key == "Name" && arg.Value.Value is string s && !string.IsNullOrWhiteSpace(s))
                        return s;
                }
                if (attr.ConstructorArguments.Length >= 1
                    && attr.ConstructorArguments[0].Value is string cs && !string.IsNullOrWhiteSpace(cs))
                    return cs;
            }
        }

        // default: <Namespace>.<TrimmedName>, mirroring ServiceBinder.GetDefaultName
        var trimmed = iface.Name;
        if (trimmed.Length > 1 && trimmed[0] == 'I' && char.IsUpper(trimmed[1])) trimmed = trimmed.Substring(1);
        var ns = iface.ContainingNamespace.IsGlobalNamespace ? "" : iface.ContainingNamespace.ToDisplayString();
        return string.IsNullOrEmpty(ns) ? trimmed : ns + "." + trimmed;
    }

    private static string MakeSanitizedBase(INamedTypeSymbol iface)
    {
        var ns = iface.ContainingNamespace.IsGlobalNamespace ? "" : iface.ContainingNamespace.ToDisplayString();
        var raw = (string.IsNullOrEmpty(ns) ? "" : ns + ".") + iface.Name;
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }
        return sb.ToString();
    }

    private static string SanitizeHintName(string fullName)
    {
        var sb = new System.Text.StringBuilder(fullName.Length);
        foreach (var ch in fullName)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' || ch == '.' ? ch : '_');
        }
        return sb.ToString();
    }

    private sealed record Candidate(InterfaceModel? Model, ImmutableArray<Diagnostic> Diagnostics);
}
