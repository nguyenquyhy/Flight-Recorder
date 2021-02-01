using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace FlightRecorder.Client.Generators
{
    [Generator]
    public class ModelGenerator : BaseGenerator, ISourceGenerator
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
            var fields = GetAircraftFields(context).ToList();

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
            foreach ((_, var name, _, _, _, _) in fields)
            {
                builder.Append($@"
                {name} = s.{name},");
            }
            builder.Append(@"
            };
");

            builder.Append(@"
        public static partial AircraftPositionStruct ToStruct(AircraftPosition s)
            => new AircraftPositionStruct
            {");
            foreach ((_, var name, _, _, _, _) in fields)
            {
                builder.Append($@"
                {name} = s.{name},");
            }
            builder.Append(@"
            };
");

            foreach ((var type, var name, _, _, _, _) in fields)
            {
                builder.Append($@"
        public {type} {name} {{ get; set; }}");
            }

            builder.Append(@"
    }
}");

            context.AddSource("ViewModelGenerator", SourceText.From(builder.ToString(), Encoding.UTF8));
        }
    }
}
