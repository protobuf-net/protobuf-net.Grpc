using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using ProtoBuf.Grpc.Generator.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ProtoBuf.Grpc.Generator
{
    [Generator]
    public partial class ClientGenerator : ISourceGenerator
    {
        [Conditional("DEBUG")]
        internal static void DebugAppendLog(string message)
        {
            //#if DEBUG
            //            try
            //            {
            //                File.AppendAllText(@"c:\Code\thinking.log", message + Environment.NewLine);
            //            }
            //            catch { }
            //#endif
        }

        void ISourceGenerator.Initialize(InitializationContext context)
            => context.RegisterForSyntaxNotifications(() => new ServiceReceiver());

        void ISourceGenerator.Execute(SourceGeneratorContext context)
        {
            try
            {
                var services = (context.SyntaxReceiver as ServiceReceiver)?.Services;
                if (services is object)
                {
                    foreach (var service in services) Execute(context, service);
                }
            }
            catch (Exception ex)
            {
                DebugAppendLog(ex.ToString());
            }
        }
        private void Execute(in SourceGeneratorContext context, InterfaceDeclarationSyntax service)
        {
            DebugAppendLog($"generating {service.Identifier}");

            var options = (CSharpParseOptions)service.SyntaxTree.Options;
            int indent = 0;
            var sb = new StringBuilder();

            // get into the correct location in the type/namespace hive
            var typeContext = TypeContext.For(service, TypeContext.Order.OutermostFirst);

            if (typeContext.CompilationUnit?.Usings.Any() == true)
            {
                foreach (var item in typeContext.CompilationUnit.Usings)
                {
                    sb.Append(item);
                    NewLine();
                }
                NewLine();
            }

            sb.Append("// ").Append(string.Join(", ", options.PreprocessorSymbolNames));
            NewLine();

            foreach (var item in typeContext.Namespaces)
            {
                sb.Append("namespace ").Append(item.Name);
                StartBlock();
                foreach (var usingDirective in item.Usings)
                {
                    NewLine().Append(usingDirective);
                }
            }

            foreach (var type in typeContext.Types)
            {
                NewLine().Append("partial class ").Append(type.Identifier.Text);
                StartBlock();
            }

            // add an attribute to the interface definition
            NewLine().Append($@"[global::ProtoBuf.Grpc.Configuration.Proxy(typeof(__{service.Identifier}__GeneratedProxy))]");
            NewLine().Append($"partial interface {service.Identifier}");

            if (options.LanguageVersion >= LanguageVersion.CSharp8
                && context.Compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.RuntimeFeature")?.GetMembers("DefaultImplementationsOfInterfaces").Any() == true)
            {   // implementation can be nested
                StartBlock();
                WriteProxy(Accessibility.Private);
                NewLine().Append($"public static {service.Identifier} Create(global::Grpc.Core.CallInvoker callInvoker) => new __{service.Identifier}__GeneratedProxy(callInvoker);");
                NewLine().Append($"public static {service.Identifier} Create(global::Grpc.Core.ChannelBase channel) => new __{service.Identifier}__GeneratedProxy(channel.CreateCallInvoker());");
                EndBlock();
            }
            else
            {
                StartBlock();
                EndBlock();
                WriteProxy(typeContext.Types.Any() ? Accessibility.Private : Accessibility.Internal);
            }

            void WriteProxy(Accessibility accessibility)
            {
                // declare an internal type that implements the interface
                NewLine().Append($"{ToSyntax(accessibility)} sealed class __{service.Identifier}__GeneratedProxy : global::Grpc.Core.ClientBase, {service.Identifier}");

                // write the actual service implementations
                StartBlock();
                NewLine().Append($"internal __{service.Identifier}__GeneratedProxy(global::Grpc.Core.CallInvoker channel) : base(channel) {{}}");

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

                static string ToSyntax(Accessibility accessibility)
                    => accessibility switch {
                        Accessibility.Public => "public",
                        Accessibility.Private => "private",
                        Accessibility.ProtectedAndInternal => "private protected",
                        Accessibility.Protected => "protected",
                        Accessibility.Internal => "internal",
                        Accessibility.ProtectedOrInternal => "protected internal",
                        _ => throw new ArgumentException($"Invalid accessibility: {accessibility}", nameof(accessibility)),
                    };
            }

            // get back out of the type/namespace hive
            foreach (var _ in typeContext.Types) EndBlock();
            foreach (var _ in typeContext.Namespaces) EndBlock();

            // add the generated content
            var code = sb.ToString();
            context.AddSource($"{service.Identifier}.Generated.cs", SourceText.From(code, Encoding.UTF8));
            //#if DEBUG   // lazy hack to show what we did
            //            File.WriteAllText(@$"c:\code\generator_output_{service.Identifier}_{Guid.NewGuid()}.cs", code, Encoding.UTF8);
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
        public List<InterfaceDeclarationSyntax> Services { get; } = new List<InterfaceDeclarationSyntax>();

        void ISyntaxReceiver.OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is InterfaceDeclarationSyntax iService
                && iService.AttributeLists.Any()) // note that this is explicitly a C# node
            {
                var context = TypeContext.For(iService, TypeContext.Order.InnermostFirst);
                foreach (var attribList in iService.AttributeLists)
                {
                    foreach (var attrib in attribList.Attributes)
                    {
                        bool result = IsAttribute(context, attrib, "ProtoBuf.Grpc.Configuration.GenerateProxyAttribute");
                        ClientGenerator.DebugAppendLog($"{iService.Identifier} [{attrib.Name.ToFullString()}]: {result}");
                        if (result)
                        {
                            Services.Add(iService);
                            return; // we're done
                        }
                    }
                }
            }
        }

        //private static bool IsType(TypeSyntax type, string fullyQualifiedName)
        //    => IsType(type.ToFullString(), fullyQualifiedName, type);

        private static bool IsAttribute(in TypeContext context, AttributeSyntax attribute, string fullyQualifiedName)
        {
            var localName = attribute.Name.ToFullString();
            if (IsType(context, localName, fullyQualifiedName)) return true;

            if (fullyQualifiedName.EndsWith("Attribute"))
            {
                fullyQualifiedName = fullyQualifiedName.Substring(0, fullyQualifiedName.Length - 9);
                if (IsType(context, localName, fullyQualifiedName)) return true;
            }
            return false;
        }
        private static bool IsType(in TypeContext context, string localName, string fullyQualifiedName)
        {
            // check the name respecting using directives (including aliases)
            foreach(var ns in context.Namespaces)
            {
                var result = CheckUsings(localName, ns.Usings, fullyQualifiedName);
                if (result.HasValue) return result.Value;
            }
            if (context.CompilationUnit is object)
            {
                var result = CheckUsings(localName, context.CompilationUnit.Usings, fullyQualifiedName);
                if (result.HasValue) return result.Value;
            }

            // finally, check the full name *without* namespaces (could be fully qualified)
            return IsMatch(localName, fullyQualifiedName);

            static bool? CheckUsings(string name, SyntaxList<UsingDirectiveSyntax> usings, string expected)
            {
                foreach (var u in usings)
                {
                    var alias = u.Alias?.Name.Identifier.ValueText;
                    var ns = u.Name.ToFullString();
                    if (alias is string)
                    {
                        if (alias == name)
                        {   // exact alias match; don't need to check other usings
                            return IsMatch(ns, expected);
                        }
                        else if (name.StartsWith(alias + "."))
                        {   // partial alias match; alias is Foo, attribute could be Foo.Bar
                            if (IsMatch(ns + name.Substring(alias.Length), expected))
                                return true;
                        }
                    }
                    else
                    {
                        if (IsMatch(ns + "." + name, expected))
                            return true;
                    }
                }
                return default;
            }
            static bool IsMatch(string actual, string expected)
            {
                int i = actual.IndexOf("::"); // strip any root qualifier
                if (i >= 0) actual = actual.Substring(i + 2);
                return actual == expected;
            }
        }
    }    
}
