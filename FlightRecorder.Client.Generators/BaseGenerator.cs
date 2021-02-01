using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace FlightRecorder.Client.Generators
{
    public abstract class BaseGenerator
    {
        protected IEnumerable<(string type, string name)> GetAircraftFields(GeneratorExecutionContext context)
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
