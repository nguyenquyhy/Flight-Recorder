using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace FlightRecorder.Client.Generators
{
    [Generator]
    public class ViewModelGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
#if DEBUG
            if (!Debugger.IsAttached)
            {
                //Debugger.Launch();
            }
#endif
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var fields = GetFields(context).ToList();

            var builder = new StringBuilder();
            builder.Append(@"
using System;
using FlightRecorder.Client;

namespace FlightRecorder.Client
{
    public partial class AircraftPosition
    {");

            builder.Append(@"
        public static partial AircraftPosition FromStruct(AircraftPositionStruct s)
            => new AircraftPosition
            {");
            foreach ((_, var name) in fields)
            {
                builder.Append($@"
                {name} = s.{name},");
            }
            builder.Append(@"
            };
");

            foreach ((var type, var name) in fields)
            {
                builder.Append($@"
        public {type} {name} {{ get; set; }}");
            }

            builder.Append(@"
    }
}");

            context.AddSource("ViewModelGenerator", SourceText.From(builder.ToString(), Encoding.UTF8));
        }

        private IEnumerable<(string type, string name)> GetFields(GeneratorExecutionContext context)
        {
            //var libraryContext = context.Compilation.References.FirstOrDefault(r => r.Display == "FlightRecorder.Client.SimConnectMSFS") as CompilationReference;
            var libraryContext = context;

            foreach (var tree in libraryContext.Compilation.SyntaxTrees)
            {
                foreach (var str in tree.GetRoot().DescendantNodesAndSelf().OfType<StructDeclarationSyntax>())
                {
                    if (str.Identifier.ValueText == "AircraftPositionStruct")
                    {
                        foreach (var field in str.Members.OfType<FieldDeclarationSyntax>())
                        {
                            if (field.Declaration.Type is PredefinedTypeSyntax predefinedType)
                            {
                                yield return (predefinedType.Keyword.ValueText, field.Declaration.Variables[0].Identifier.ValueText);
                            }
                        }
                    }
                }
            }
        }
    }
}
