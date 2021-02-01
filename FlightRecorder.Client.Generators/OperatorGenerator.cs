using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace FlightRecorder.Client.Generators
{
    [Generator]
    public class OperatorGenerator : BaseGenerator, ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
#if DEBUG
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
#endif
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var fields = GetAircraftFields(context).ToList();

            var builder = new StringBuilder();
            builder.Append(@"
using System;
using FlightRecorder.Client;

namespace FlightRecorder.Client
{
    public partial class AircraftPositionStructOperator
    {");

            builder.Append(@"
        public static partial AircraftPositionStruct Add(AircraftPositionStruct position1, AircraftPositionStruct position2)
            => new AircraftPositionStruct
            {");
            foreach ((_, var name) in fields)
            {
                builder.Append($@"
                {name} = position1.{name} + position2.{name},");
            }
            builder.Append(@"
            };
");


            builder.Append(@"
        public static partial AircraftPositionStruct Scale(AircraftPositionStruct position, double factor)
            => new AircraftPositionStruct
            {");
            foreach ((_, var name) in fields)
            {
                builder.Append($@"
                {name} = position.{name} * factor,"); // TODO: support wrapping around for angle
            }
            builder.Append(@"
            };
");

            builder.Append(@"
    }
}");

            context.AddSource("ViewModelGenerator", SourceText.From(builder.ToString(), Encoding.UTF8));
        }
    }
}
