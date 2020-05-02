using Google.Protobuf.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using ProtoBuf.Reflection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;

namespace ProtoBuf.Generator
{
    [Generator]
    public class Protogen : ISourceGenerator
    {
        public void Initialize(InitializationContext context) { }

        static CodeGenerator GetCodeGenerator(in SourceGeneratorContext context, out Dictionary<string, string> options)
        {
            options = new Dictionary<string, string>
            {
                {"services", "yes" },
                {"listset", "no" }
            };
            if (context.Compilation.Language == "Visual Basic") return VBCodeGenerator.Default;

            // default to C#
            options.Add("langver", "8.0");
            return CSharpCodeGenerator.Default;
        }
        public void Execute(SourceGeneratorContext context)
        {
            try
            {
                ExecuteImpl(context);
            }
            catch (Exception ex)
            {
                DebugAppendLog(ex.Message);
            }
        }

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

        private void ExecuteImpl(in SourceGeneratorContext context)
        {
            // find anything that matches our files
            var files = context.AdditionalFiles.Where(at => at.Path.EndsWith(".proto"));
            if (files.Any())
            {
                DebugAppendLog($"Initializing...");
                var codegen = GetCodeGenerator(context, out var options);

                FileDescriptorSet set = new FileDescriptorSet();
                foreach (var file in files)
                {
                    DebugAppendLog($"Adding {file}...");
                    var content = file.GetText(context.CancellationToken);
                    if (content == null) continue;

                    // set.AddImportPath(Path.GetDirectoryName(file.Path));
                    set.Add(file.Path, true, new StringReader(content.ToString()));
                }
                DebugAppendLog($"Processing...");
                set.Process();
                DebugAppendLog($"Checking errors...");
                var errors = set.GetErrors();
                bool generate = true;
                foreach (var error in errors)
                {
                    DebugAppendLog(error.ToString());
                    int line = error.LineNumber, col = error.ColumnNumber, width = error.Text?.Length ?? 0;
                    var location = Location.Create(error.File,
                        default,
                        new LinePositionSpan(new LinePosition(line, col), new LinePosition(line, col + width)));
                    DiagnosticSeverity severity = DiagnosticSeverity.Info;
                    int warnLevel = 0;
                    if (error.IsError)
                    {
                        severity = DiagnosticSeverity.Error;
                        generate = false;
                    }
                    else if (error.IsWarning)
                    {
                        severity = DiagnosticSeverity.Warning;
                        warnLevel = 1;
                    }

                    context.ReportDiagnostic(Diagnostic.Create("PBN" + error.ErrorNumber, "protogen", error.Message,
                        severity, severity, true, warnLevel, location: location));
                }
                if (generate)
                {
                    DebugAppendLog("Generating...");
                    foreach (var output in codegen.Generate(set, options: options))
                    {
                        DebugAppendLog($"Adding {output.Name}...");
                        context.AddSource(output.Name, SourceText.From(output.Text, Encoding.UTF8));
                    }
                }
                DebugAppendLog("All done");
            }
        }
    }
}
