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
            var simStateFields = GetSimConnectFields(context, SimState).ToList();
            var aircraftFields = GetSimConnectFields(context, AircraftPosition).ToList();

            var builder = new StringBuilder();
            builder.Append(@"
using System;
using FlightRecorder.Client;

namespace FlightRecorder.Client
{
    public partial class SimState
    {");

            builder.Append(@"
        public static SimState FromStruct(SimStateStruct s)
            => new SimState
            (");
            builder.Append(string.Join(",", simStateFields.Select(item => $@"
                s.{item.name}")));
            builder.Append(@"
            );
");

            builder.Append(@"
        public static SimStateStruct ToStruct(SimState s)
            => new SimStateStruct
            {");
            foreach ((_, var name, _, _, _, _, _, _, _) in simStateFields)
            {
                builder.Append($@"
                {name} = s.{name},");
            }
            builder.Append(@"
            };
");

            // Constructors
            builder.Append(@"
        public SimState(");
            builder.Append(string.Join(", ", simStateFields.Select(item => $"{item.type} {item.name}")));
            builder.Append(@")
        {");
            foreach ((var type, var name, _, _, _, _, _, _, _) in simStateFields)
            {
                builder.Append($@"
            this.{name} = {name};");
            }
            builder.Append(@"
        }
");

            // Properties
            foreach ((var type, var name, _, _, _, _, _, _, _) in simStateFields)
            {
                builder.Append($@"
        public {type} {name} {{ get; set; }}");
            }

            builder.Append(@"
    }

    public partial class AircraftPosition
    {");

            builder.Append(@"
        public static AircraftPosition FromStruct(AircraftPositionStruct s)
            => new AircraftPosition
            {");
            foreach ((_, var name, _, _, _, _, _, _, _) in aircraftFields)
            {
                builder.Append($@"
                {name} = s.{name},");
            }
            builder.Append(@"
            };
");

            builder.Append(@"
        public static AircraftPositionStruct ToStruct(AircraftPosition s)
            => new AircraftPositionStruct
            {");
            foreach ((_, var name, _, _, _, _, _, _, _) in aircraftFields)
            {
                builder.Append($@"
                {name} = s.{name},");
            }
            builder.Append(@"
            };
");

            builder.Append(@"
        public long Milliseconds { get; set; }");

            foreach ((var type, var name, _, _, _, _, _, _, _) in aircraftFields)
            {
                builder.Append($@"
        public {type} {name} {{ get; set; }}");
            }

            builder.Append(@"
    }
}");

            context.AddSource("ModelGenerator", SourceText.From(builder.ToString(), Encoding.UTF8));
        }
    }
}
