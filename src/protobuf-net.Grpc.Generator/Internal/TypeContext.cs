using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace ProtoBuf.Grpc.Generator.Internal
{
    internal readonly struct TypeContext
    {
        public enum Order
        {
            OutermostFirst,
            InnermostFirst,
        }
        public SyntaxNode? Node { get; }
        public ImmutableArray<TypeDeclarationSyntax> Types { get; }
        public ImmutableArray<NamespaceDeclarationSyntax> Namespaces { get; }
        public CompilationUnitSyntax? CompilationUnit { get; }

        public TypeContext(SyntaxNode? original, ImmutableArray<TypeDeclarationSyntax> types, ImmutableArray<NamespaceDeclarationSyntax> namespaces, CompilationUnitSyntax? file)
        {
            Node = original;
            Types = types;
            Namespaces = namespaces;
            CompilationUnit = file;
        }

        // need to get the correct namespace etc; some useful context here: https://stackoverflow.com/a/61409409/23354
        public static TypeContext For(SyntaxNode? node, Order order)
        {
            var original = node;
            ImmutableArray<TypeDeclarationSyntax>.Builder? types = null;
            ImmutableArray<NamespaceDeclarationSyntax>.Builder? namespaces = null;
            CompilationUnitSyntax? file = null;

            while ((node = node?.Parent) is object)
            {
                switch (node)
                {
                    case NamespaceDeclarationSyntax ns:
                        namespaces ??= ImmutableArray.CreateBuilder<NamespaceDeclarationSyntax>();
                        namespaces.Add(ns);
                        break;
                    case TypeDeclarationSyntax type:
                        types ??= ImmutableArray.CreateBuilder<TypeDeclarationSyntax>();
                        types.Add(type);
                        break;
                    case CompilationUnitSyntax cus:
                        file = cus;
                        break;
                }
            }

            if (order == Order.OutermostFirst)
            {
                types?.Reverse();
                namespaces?.Reverse();
            }
            return new TypeContext(original,
                types?.ToImmutableArray() ?? ImmutableArray<TypeDeclarationSyntax>.Empty,
                namespaces?.ToImmutableArray() ?? ImmutableArray<NamespaceDeclarationSyntax>.Empty,
                file);
        }
    }
}
