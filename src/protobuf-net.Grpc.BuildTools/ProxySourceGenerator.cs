using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ProtoBuf.Grpc.BuildTools;

[Generator(LanguageNames.CSharp)]
public sealed class ProxySourceGenerator : IIncrementalGenerator
{
    private const string GenerateProxyAttributeFullName = "ProtoBuf.Grpc.Configuration.GenerateProxyAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenerateProxyAttributeFullName,
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, ct) => BuildCandidate(ctx, ct))
            .Where(static x => x is not null)!;

        context.RegisterSourceOutput(candidates, static (ctx, candidate) =>
        {
            if (candidate is null) return;

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
        });
    }

    private static Candidate? BuildCandidate(GeneratorAttributeSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (ctx.TargetSymbol is not INamedTypeSymbol iface || iface.TypeKind != TypeKind.Interface)
            return null;

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        // diagnostic: must be top-level
        if (iface.ContainingType is not null)
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.InterfaceMustNotBeNested,
                iface.Locations.FirstOrDefault(),
                iface.Name,
                iface.ContainingType.ToDisplayString()));
            return new Candidate(null, diagnostics.ToImmutable());
        }

        // diagnostic: generic interfaces not supported
        if (iface.IsGenericType)
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.GenericInterfaceNotSupported,
                iface.Locations.FirstOrDefault(),
                iface.Name));
            return new Candidate(null, diagnostics.ToImmutable());
        }

        // diagnostic: must be partial
        bool isPartial = false;
        foreach (var decl in iface.DeclaringSyntaxReferences)
        {
            if (decl.GetSyntax(ct) is InterfaceDeclarationSyntax syn
                && syn.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                isPartial = true;
                break;
            }
        }
        if (!isPartial)
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.InterfaceMustBePartial,
                iface.Locations.FirstOrDefault(),
                iface.Name));
            return new Candidate(null, diagnostics.ToImmutable());
        }

        // walk all methods on this interface and its base interfaces (mirrors ContractOperation.ExpandInterfaces)
        var allInterfaces = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        allInterfaces.Add(iface);
        foreach (var i in iface.AllInterfaces) allInterfaces.Add(i);

        var ops = ImmutableArray.CreateBuilder<OperationModel>();
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
                    diagnostics.Add(Diagnostic.Create(
                        Diagnostics.UnsupportedMethodShape,
                        method.Locations.FirstOrDefault(),
                        i.ToDisplayString(),
                        method.Name));
                }
            }
        }

        var serviceName = GetServiceName(iface);
        var nsName = iface.ContainingNamespace.IsGlobalNamespace ? "" : iface.ContainingNamespace.ToDisplayString();
        var proxyTypeName = MakeProxyTypeName(iface);
        var proxyFullTypeName = "ProtoBuf.Grpc.Generated." + proxyTypeName;
        var accessibility = iface.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.NotApplicable => "internal",
            _ => "internal",
        };

        var model = new InterfaceModel(
            Namespace: nsName,
            InterfaceName: iface.Name,
            InterfaceFullName: iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            InterfaceAccessibility: accessibility,
            ServiceName: serviceName,
            ProxyTypeName: proxyTypeName,
            ProxyFullTypeName: proxyFullTypeName,
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

    private static string MakeProxyTypeName(INamedTypeSymbol iface)
    {
        var ns = iface.ContainingNamespace.IsGlobalNamespace ? "" : iface.ContainingNamespace.ToDisplayString();
        var raw = (string.IsNullOrEmpty(ns) ? "" : ns + ".") + iface.Name;
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }
        return sb.ToString() + "_ClientProxy";
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
