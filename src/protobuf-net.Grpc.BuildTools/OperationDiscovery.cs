using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ProtoBuf.Grpc.BuildTools;

internal static class OperationDiscovery
{
    private static readonly SymbolDisplayFormat s_fullyQualified = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
            | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static string Display(this ITypeSymbol type) => type.ToDisplayString(s_fullyQualified);

    public static bool TryBuild(IMethodSymbol method, out OperationModel? model)
    {
        model = null;
        if (method.IsGenericMethod) return false; // <T> methods are not supported
        if (method.MethodKind != Microsoft.CodeAnalysis.MethodKind.Ordinary) return false;
        if (method.Parameters.Length > 3) return false;

        // operation name: [Operation(Name="X")] -> X; else trim trailing "Async"
        var opName = TryGetOperationName(method) ?? StripAsync(method.Name);

        var returnInfo = CategorizeReturn(method.ReturnType);
        if (returnInfo is null) return false;

        var (responseShape, methodKindFromReturn, responseType, voidResponse) = returnInfo.Value;

        // Parameters: optionally a request (data or IAsyncEnumerable<>) and optionally a context
        ArgShape requestShape;
        string requestType;
        bool voidRequest;
        ContextKind context;
        MethodKind methodKind;

        IParameterSymbol? first = method.Parameters.Length >= 1 ? method.Parameters[0] : null;
        IParameterSymbol? second = method.Parameters.Length >= 2 ? method.Parameters[1] : null;

        // Pattern: (request?, context?)
        if (first is null)
        {
            requestShape = ArgShape.Void;
            requestType = "global::ProtoBuf.Grpc.Internal.Empty";
            voidRequest = true;
            context = ContextKind.None;
        }
        else
        {
            var firstKind = CategorizeArg(first.Type);
            if (firstKind == ArgKind.Context)
            {
                // (context)
                requestShape = ArgShape.Void;
                requestType = "global::ProtoBuf.Grpc.Internal.Empty";
                voidRequest = true;
                context = first.Type.SpecialType == SpecialType.System_Object ? ContextKind.None : MapContext(first.Type);
                if (second is not null) return false; // can't have anything after context
            }
            else if (firstKind == ArgKind.Data)
            {
                requestShape = ArgShape.Data;
                requestType = first.Type.Display();
                voidRequest = false;
                if (second is null)
                {
                    context = ContextKind.None;
                }
                else
                {
                    var secondKind = CategorizeArg(second.Type);
                    if (secondKind != ArgKind.Context) return false;
                    context = MapContext(second.Type);
                    if (method.Parameters.Length > 2) return false;
                }
            }
            else if (firstKind == ArgKind.AsyncEnumerable)
            {
                requestShape = ArgShape.AsyncEnumerable;
                requestType = GetElementType(first.Type)!.Display();
                voidRequest = false;
                if (second is null)
                {
                    context = ContextKind.None;
                }
                else
                {
                    var secondKind = CategorizeArg(second.Type);
                    if (secondKind != ArgKind.Context) return false;
                    context = MapContext(second.Type);
                    if (method.Parameters.Length > 2) return false;
                }
            }
            else
            {
                return false; // unsupported parameter kind
            }
        }

        // figure out final MethodKind by combining request/response
        methodKind = methodKindFromReturn switch
        {
            MethodKind.ServerStreaming => requestShape == ArgShape.AsyncEnumerable ? MethodKind.DuplexStreaming : MethodKind.ServerStreaming,
            MethodKind.Unary => requestShape == ArgShape.AsyncEnumerable ? MethodKind.ClientStreaming : MethodKind.Unary,
            _ => methodKindFromReturn,
        };

        // duplex requires server-streaming response shape
        if (methodKind == MethodKind.DuplexStreaming && responseShape != ResultShape.AsyncEnumerable) return false;
        // client-streaming requires non-streaming response
        if (methodKind == MethodKind.ClientStreaming && responseShape == ResultShape.AsyncEnumerable) return false;

        var parameters = ImmutableArray.CreateBuilder<ParameterModel>(method.Parameters.Length);
        foreach (var p in method.Parameters)
        {
            parameters.Add(new ParameterModel(p.Name, p.Type.Display()));
        }

        model = new OperationModel(
            OperationName: opName,
            MethodName: method.Name,
            Kind: methodKind,
            Context: context,
            RequestShape: requestShape,
            ResponseShape: responseShape,
            RequestTypeFullName: requestType,
            ResponseTypeFullName: responseType,
            VoidRequest: voidRequest,
            VoidResponse: voidResponse,
            ReturnTypeDisplay: method.ReturnType.Display(),
            Parameters: parameters.MoveToImmutable(),
            Location: method.Locations.Length > 0 ? method.Locations[0] : null);
        return true;
    }

    private static string? TryGetOperationName(IMethodSymbol method)
    {
        foreach (var attr in method.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null) continue;
            if (attrClass.Name == "OperationContractAttribute" || attrClass.Name == "OperationAttribute")
            {
                foreach (var arg in attr.NamedArguments)
                {
                    if (arg.Key == "Name" && arg.Value.Value is string name && !string.IsNullOrWhiteSpace(name))
                        return name;
                }
                if (attr.ConstructorArguments.Length == 1 && attr.ConstructorArguments[0].Value is string ctorName && !string.IsNullOrWhiteSpace(ctorName))
                    return ctorName;
            }
        }
        return null;
    }

    private static string StripAsync(string name)
        => name.EndsWith("Async", System.StringComparison.Ordinal) && name.Length > "Async".Length
            ? name.Substring(0, name.Length - "Async".Length)
            : name;

    private enum ArgKind { Data, Context, AsyncEnumerable, Unknown }

    private static ArgKind CategorizeArg(ITypeSymbol type)
    {
        if (IsCallContext(type)) return ArgKind.Context;
        if (IsCancellationToken(type)) return ArgKind.Context;
        if (IsAsyncEnumerable(type)) return ArgKind.AsyncEnumerable;
        if (IsServerCallContext(type)) return ArgKind.Unknown; // server-side, skip
        if (IsCallOptions(type)) return ArgKind.Unknown;       // raw grpc client, skip
        return ArgKind.Data;
    }

    private static ContextKind MapContext(ITypeSymbol type)
    {
        if (IsCallContext(type)) return ContextKind.CallContext;
        if (IsCancellationToken(type)) return ContextKind.CancellationToken;
        return ContextKind.None;
    }

    private static (ResultShape Shape, MethodKind ImpliedKind, string DataType, bool Void)? CategorizeReturn(ITypeSymbol returnType)
    {
        // void
        if (returnType.SpecialType == SpecialType.System_Void)
            return (ResultShape.Sync, MethodKind.Unary, "global::ProtoBuf.Grpc.Internal.Empty", true);

        // Task / ValueTask (untyped)
        if (IsType(returnType, "System.Threading.Tasks", "Task") && returnType is INamedTypeSymbol untyped1 && untyped1.TypeArguments.Length == 0)
            return (ResultShape.Task, MethodKind.Unary, "global::ProtoBuf.Grpc.Internal.Empty", true);
        if (IsType(returnType, "System.Threading.Tasks", "ValueTask") && returnType is INamedTypeSymbol untyped2 && untyped2.TypeArguments.Length == 0)
            return (ResultShape.ValueTask, MethodKind.Unary, "global::ProtoBuf.Grpc.Internal.Empty", true);

        if (returnType is INamedTypeSymbol named && named.IsGenericType)
        {
            var def = named.ConstructedFrom;
            if (def.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks")
            {
                if (def.Name == "Task")
                    return (ResultShape.Task, MethodKind.Unary, named.TypeArguments[0].Display(), false);
                if (def.Name == "ValueTask")
                    return (ResultShape.ValueTask, MethodKind.Unary, named.TypeArguments[0].Display(), false);
            }
            if (def.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic" && def.Name == "IAsyncEnumerable")
            {
                return (ResultShape.AsyncEnumerable, MethodKind.ServerStreaming, named.TypeArguments[0].Display(), false);
            }
        }

        // bare sync return value
        return (ResultShape.Sync, MethodKind.Unary, returnType.Display(), false);
    }

    private static ITypeSymbol? GetElementType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol n && n.IsGenericType && n.TypeArguments.Length == 1) return n.TypeArguments[0];
        return null;
    }

    private static bool IsAsyncEnumerable(ITypeSymbol type)
        => type is INamedTypeSymbol n && n.IsGenericType
            && n.ConstructedFrom.Name == "IAsyncEnumerable"
            && n.ConstructedFrom.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic";

    private static bool IsCallContext(ITypeSymbol type)
        => type.Name == "CallContext" && type.ContainingNamespace?.ToDisplayString() == "ProtoBuf.Grpc";

    private static bool IsCancellationToken(ITypeSymbol type)
        => type.Name == "CancellationToken" && type.ContainingNamespace?.ToDisplayString() == "System.Threading";

    private static bool IsServerCallContext(ITypeSymbol type)
        => type.Name == "ServerCallContext" && type.ContainingNamespace?.ToDisplayString() == "Grpc.Core";

    private static bool IsCallOptions(ITypeSymbol type)
        => type.Name == "CallOptions" && type.ContainingNamespace?.ToDisplayString() == "Grpc.Core";

    private static bool IsType(ITypeSymbol type, string ns, string name)
        => type.Name == name && type.ContainingNamespace?.ToDisplayString() == ns;
}
