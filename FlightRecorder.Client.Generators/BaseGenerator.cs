using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace FlightRecorder.Client.Generators
{
    public abstract class BaseGenerator
    {
        protected const int SetTypeDefault = 0; // TODO: replace 0
        protected const int SetTypeEvent = 1;
        protected const int SetTypeNone = 2;

        protected IEnumerable<(string type, string name, string variable, string unit, int dataType, int? setType, string setEventName)> GetAircraftFields(GeneratorExecutionContext context)
        {
            //var libraryContext = context.Compilation.References.FirstOrDefault(r => r.Display == "FlightRecorder.Client.SimConnectMSFS") as CompilationReference;
            var libraryContext = context;

            var simconnectVariableAttributeType = context.Compilation.GetTypeByMetadataName("FlightRecorder.Client.SimConnectVariableAttribute");

            foreach (var tree in libraryContext.Compilation.SyntaxTrees)
            {
                var semanticModel = libraryContext.Compilation.GetSemanticModel(tree);

                foreach (var str in tree.GetRoot().DescendantNodesAndSelf().OfType<StructDeclarationSyntax>())
                {
                    if (str.Identifier.ValueText == "AircraftPositionStruct")
                    {
                        foreach (var field in str.Members.OfType<FieldDeclarationSyntax>())
                        {
                            if (semanticModel.GetDeclaredSymbol(field.Declaration.Variables[0]) is IFieldSymbol fieldSymbol)
                            {
                                var attributes = fieldSymbol.GetAttributes();
                                var simconnectVariableAttribute = attributes.First(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, simconnectVariableAttributeType));

                                if (field.Declaration.Type is PredefinedTypeSyntax predefinedType)
                                {
                                    var args = simconnectVariableAttribute.NamedArguments;
                                    var variable = args.First(arg => arg.Key == "Name").Value.Value as string;
                                    var unit = args.First(arg => arg.Key == "Unit").Value.Value as string;
                                    var type = args.First(arg => arg.Key == "Type").Value.Value as int?;
                                    var setType = args.FirstOrDefault(arg => arg.Key == "SetType").Value.Value as int?;
                                    var setBy = args.Any(args => args.Key == "SetByEvent") ? args.First(arg => arg.Key == "SetByEvent").Value.Value as string : null;
                                    yield return (predefinedType.Keyword.ValueText, field.Declaration.Variables[0].Identifier.ValueText, variable, unit, type.Value, setType, setBy);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
