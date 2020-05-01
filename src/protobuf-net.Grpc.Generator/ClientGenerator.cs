using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ProtoBuf.Grpc.Generator
{
    [Generator]
    public class ClientGenerator : ISourceGenerator
    {
        void ISourceGenerator.Initialize(InitializationContext context)
            => context.RegisterForSyntaxNotifications(() => new ServiceReceiver());

        void ISourceGenerator.Execute(SourceGeneratorContext context)
        {
            var service = (context.SyntaxReceiver as ServiceReceiver)?.Service;
            if (service is null) return;

            var options = (CSharpParseOptions)service.SyntaxTree.Options;
            int indent = 0;
            var sb = new StringBuilder();

            // get into the correct location in the type/namespace hive
            var fqn = FullyQualifiedName.For(service);

            if (fqn.File?.Usings.Any() == true)
            {
                foreach (var item in fqn.File.Usings)
                {
                    sb.Append(item);
                    NewLine();
                }
                NewLine();
            }

            sb.Append("// ").Append(string.Join(", ", options.PreprocessorSymbolNames));
            NewLine();

            if (fqn.Namespaces?.Any() == true)
            {
                foreach (var item in fqn.Namespaces)
                {
                    sb.Append("namespace ").Append(item.Name);
                    StartBlock();
                    foreach(var usingDirective in item.Usings)
                    {
                        NewLine().Append(usingDirective);
                    }
                }
            }

            if (fqn.Types?.Any() == true)
            {
                foreach (var type in fqn.Types)
                {
                    NewLine().Append("partial class ").Append(type.Identifier.Text);
                    StartBlock();
                }
            }

            // add an attribute to the interface definition
            NewLine().Append($@"[global::ProtoBuf.Grpc.Configuration.Proxy(typeof(__{service.Identifier}__GeneratedProxy))]");
            NewLine().Append($"partial interface {service.Identifier}");
            //if (options.LanguageVersion >= LanguageVersion.CSharp8)
            //{   // implementation can be nested - turns out this isn't a good idea because of TFM support for default interface implementation, which is what it needs
            //    StartBlock();
            //    WriteProxy();
            //    NewLine().Append($"public static {service.Identifier} Create(global::Grpc.Core.ChannelBase channel) => new __{service.Identifier}__GeneratedProxy(channel);");
            //    EndBlock();
            //}
            //else
            {
                StartBlock();
                EndBlock();
                WriteProxy();
            }

            void WriteProxy()
            {
                // declare an internal type that implements the interface
                NewLine().Append($"internal sealed class __{service.Identifier}__GeneratedProxy : global::Grpc.Core.ClientBase, {service.Identifier}");

                // write the actual service implementations
                StartBlock();
                NewLine().Append($"internal __{service.Identifier}__GeneratedProxy(global::Grpc.Core.ChannelBase channel) : base(channel) {{}}");

                foreach (var member in service.Members)
                {
                    if (member is MethodDeclarationSyntax method)
                    {
                        // write a method implementation; we'll use implicit implementation for simplicity
                        NewLine().Append("public ").Append(method.ReturnType);
                        sb.Append(" ").Append(method.Identifier).Append('(');
                        bool first = true;
                        foreach (var arg in method.ParameterList.Parameters)
                        {
                            if (first) first = false;
                            else sb.Append(", ");
                            sb.Append(arg.Type!).Append(" ").Append(arg.Identifier);
                        }
                        sb.Append(")");

                        // call into gRPC; don't worry about this bit for now
                        NewLine().Append($"\t=> throw new global::System.NotImplementedException();");
                    }
                    // not too concerned about other interface member types
                    // TODO: pre-screen for validity?
                }
                EndBlock();
            }

            // get back out of the type/namespace hive
            if (fqn.Types?.Any() == true)
            {
                foreach (var _ in fqn.Types) EndBlock();
            }
            if (fqn.Namespaces?.Any() == true)
            {
                foreach (var _ in fqn.Namespaces) EndBlock();
            }

            // add the generated content
            var code = sb.ToString();
            context.AddSource($"{service.Identifier}.Generated.cs", SourceText.From(code, Encoding.UTF8));
//#if DEBUG   // lazy hack to show what we did
//            File.WriteAllText(@$"c:\code\generator_output_{Guid.NewGuid()}.cs", code, Encoding.UTF8);
//#endif

            // utility methods for working with the generator
            StringBuilder NewLine()
                => sb!.AppendLine().Append('\t', indent);
            StringBuilder StartBlock()
            {
                NewLine().Append("{");
                indent++;
                return sb;
            }
            StringBuilder EndBlock()
            {
                indent--;
                NewLine().Append("}");
                return NewLine();
            }
        }        
    }

    internal sealed class ServiceReceiver : ISyntaxReceiver
    {
        public InterfaceDeclarationSyntax? Service { get; private set; }

        void ISyntaxReceiver.OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is InterfaceDeclarationSyntax iService) // note that this is explicitly a C# node
            {
                foreach (var attribList in iService.AttributeLists)
                {
                    foreach (var attrib in attribList.Attributes)
                    {
                        bool result = ShouldGenerateProxy(attrib);
                        if (result)
                        {
                            Service = iService;
                            return; // we're done
                        }
                    }
                }
            }
        }

        private static bool ShouldGenerateProxy(AttributeSyntax attrib)
        {
            var name = attrib.Name.ToFullString();

            SyntaxNode? node = attrib;
            while (node is object)
            {
                switch (node)
                {
                    case NamespaceDeclarationSyntax ns:
                        var result = CheckUsings(name, ns.Usings);
                        if (result.HasValue) return result.Value;
                        break;
                    case CompilationUnitSyntax cus:
                        result = CheckUsings(name, cus.Usings);
                        if (result.HasValue) return result.Value;
                        break;
                }
                node = node.Parent;
            }

            // finally, check the full name *without* namespaces (could be fully qualified)
            return IsGenerateProxyAttribute(name);

            static bool? CheckUsings(string name, SyntaxList<UsingDirectiveSyntax> usings)
            {
                foreach (var u in usings)
                {
                    var alias = u.Alias?.Name.Identifier.ValueText;
                    var ns = u.Name.ToFullString();
                    if (alias is string)
                    {
                        if (alias == name)
                        {   // exact alias match; don't need to check other usings
                            return IsGenerateProxyAttribute(ns);
                        }
                        else if (name.StartsWith(alias + "."))
                        {   // partial alias match; alias is Foo, attribute could be Foo.Bar
                            if (IsGenerateProxyAttribute(ns + name.Substring(alias.Length)))
                                return true;
                        }
                    }
                    else
                    {
                        if (IsGenerateProxyAttribute(ns + "." + name))
                            return true;
                    }
                }
                return default;
            }
            static bool IsGenerateProxyAttribute(string fullyQualifiedName)
            {
                int i = fullyQualifiedName.IndexOf("::"); // strip any root qualifier
                if (i >= 0) fullyQualifiedName = fullyQualifiedName.Substring(i + 2);
                switch (fullyQualifiedName)
                {
                    case "ProtoBuf.Grpc.Configuration.GenerateProxy":
                    case "ProtoBuf.Grpc.Configuration.GenerateProxyAttribute":
                        return true;
                    default:
                        return false;
                }
            }
        }
    }
    internal readonly struct FullyQualifiedName
    {
        public SyntaxNode? Node { get; }
        public List<TypeDeclarationSyntax>? Types { get; }
        public List<NamespaceDeclarationSyntax>? Namespaces { get; }
        public CompilationUnitSyntax? File { get; }

        public FullyQualifiedName(SyntaxNode? original, List<TypeDeclarationSyntax>? types, List<NamespaceDeclarationSyntax>? namespaces, CompilationUnitSyntax? file)
        {
            Node = original;
            Types = types;
            Namespaces = namespaces;
            File = file;
        }

        // need to get the correct namespace etc; some useful context here: https://stackoverflow.com/a/61409409/23354
        public static FullyQualifiedName For(SyntaxNode? node)
        {
            var original = node;
            List<TypeDeclarationSyntax>? types = null;
            List<NamespaceDeclarationSyntax>? namespaces = null;
            CompilationUnitSyntax? file = null;

            while ((node = node?.Parent) is object)
            {
                switch (node)
                {
                    case NamespaceDeclarationSyntax ns:
                        namespaces ??= new List<NamespaceDeclarationSyntax>();
                        namespaces.Add(ns);
                        break;
                    case TypeDeclarationSyntax type:
                        types ??= new List<TypeDeclarationSyntax>();
                        types.Add(type);
                        break;
                    case CompilationUnitSyntax cus:
                        file = cus;
                        break;
                }
            }
            types?.Reverse();
            namespaces?.Reverse();

            return new FullyQualifiedName(original, types, namespaces, file);
        }
    }
}
