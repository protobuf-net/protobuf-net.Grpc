using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace ProtoBuf.Generator
{
    [Generator]
    public class Protogen : ISourceGenerator
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Conditional("DEBUG")]
        internal static void DebugAppendLog(string message)
        {
#if DEBUG
            try
            {
                File.AppendAllText(@"c:\Code\protogen.log", message + Environment.NewLine);
            }
            catch { }
#endif
        }

        void ISourceGenerator.Initialize(InitializationContext context) { }
        void ISourceGenerator.Execute(SourceGeneratorContext context)
        {
            DebugAppendLog("!! Execute");
            var sb = new StringBuilder();
            sb.AppendLine("internal sealed class DidItRun {}");
            foreach (var file in context.AdditionalFiles)
            {
                // DebugAppendLog(file.Path);
                sb.Append("// ").AppendLine(file.Path);
            }
            var code = sb.ToString();
            DebugAppendLog(code);
            context.AddSource($"Generated.cs", SourceText.From(code, Encoding.UTF8));
            //try
            //{

            //    // ExecuteImpl(context);
            //}
            //catch (Exception ex)
            //{
            //    DebugAppendLog(ex.Message);
            //    try
            //    {
            //        context.ReportDiagnostic(Diagnostic.Create(CodePrefix + "XXX", ex.Message, DiagnosticCategory,
            //            DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 1));
            //        DebugAppendLog("Added diagnostic");
            //    }
            //    catch { }
            //}
        }

        //private static CodeGenerator GetCodeGenerator(in SourceGeneratorContext context, out Dictionary<string, string> options)
        //{
        //    options = new Dictionary<string, string>
        //    {
        //        {"services", "yes" },
        //        {"listset", "no" }
        //    };
        //    if (context.Compilation.Language == "Visual Basic") return VBCodeGenerator.Default;

        //    // default to C#
        //    options.Add("langver", "8.0");
        //    return CSharpCodeGenerator.Default;
        //}
        //const string DiagnosticCategory = "protogen", CodePrefix = "PBN";



        //[MethodImpl(MethodImplOptions.NoInlining)]
        //private void ExecuteImpl(in SourceGeneratorContext context)
        //{

        //    var files = context.AdditionalFiles.Where(at => at.Path.EndsWith(".proto"));
        //    if (files.Any())
        //    {
        //        DebugAppendLog($"Initializing...");
        //        var codegen = GetCodeGenerator(context, out var options);

        //        FileDescriptorSet set = new FileDescriptorSet();
        //        foreach (var file in files)
        //        {
        //            DebugAppendLog($"Adding {file}...");
        //            var content = file.GetText(context.CancellationToken);
        //            if (content == null) continue;

        //            // set.AddImportPath(Path.GetDirectoryName(file.Path));
        //            set.Add(file.Path, true, new StringReader(content.ToString()));
        //        }
        //        DebugAppendLog($"Processing...");
        //        set.Process();
        //        DebugAppendLog($"Checking errors...");
        //        var errors = set.GetErrors();
        //        bool generate = true;
        //        foreach (var error in errors)
        //        {
        //            DebugAppendLog(error.ToString());
        //            int line = error.LineNumber, col = error.ColumnNumber, width = error.Text?.Length ?? 0;
        //            var location = Location.Create(error.File,
        //                default,
        //                new LinePositionSpan(new LinePosition(line, col), new LinePosition(line, col + width)));
        //            DiagnosticSeverity severity = DiagnosticSeverity.Info;
        //            int warnLevel = 0;
        //            if (error.IsError)
        //            {
        //                severity = DiagnosticSeverity.Error;
        //                generate = false;
        //            }
        //            else if (error.IsWarning)
        //            {
        //                severity = DiagnosticSeverity.Warning;
        //                warnLevel = 1;
        //            }

        //            context.ReportDiagnostic(Diagnostic.Create(
        //                CodePrefix + error.ErrorNumber,
        //                DiagnosticCategory, error.Message,
        //                severity, severity, true, warnLevel, location: location));
        //        }
        //        if (generate)
        //        {
        //            DebugAppendLog("Generating...");
        //            foreach (var output in codegen.Generate(set, options: options))
        //            {
        //                DebugAppendLog($"Adding {output.Name}...");
        //                context.AddSource(output.Name, SourceText.From(output.Text, Encoding.UTF8));
        //            }
        //        }
        //        DebugAppendLog("All done");
        //    }
        //}
    }
}
