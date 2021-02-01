using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace FlightRecorder.Client.Generators
{
    [Generator]
    public class ConnectorGenerator : BaseGenerator, ISourceGenerator
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
using Microsoft.FlightSimulator.SimConnect;

namespace FlightRecorder.Client.SimConnectMSFS
{
    public partial class Connector
    {");

            builder.Append(@"
        private partial void RegisterAircraftPositionDefinition()
        {
            RegisterDataDefinition<AircraftPositionStruct>(DEFINITIONS.AircraftPosition");
            foreach ((_, _, var variable, var unit, var type, _) in fields)
            {
                builder.Append($@",
                (""{variable}"", ""{unit}"", (SIMCONNECT_DATATYPE){type})");
            }
            builder.Append(@"
            );
        }
");

            builder.Append(@"
        private partial void RegisterAircraftPositionSetDefinition()
        {
            RegisterDataDefinition<AircraftPositionSetStruct>(DEFINITIONS.AircraftPositionSet");
            foreach ((_, _, var variable, var unit, var type, var setBy) in fields)
            {
                if (string.IsNullOrEmpty(setBy))
                {
                    builder.Append($@",
                (""{variable}"", ""{unit}"", (SIMCONNECT_DATATYPE){type})");
                }
            }
            builder.Append(@"
            );
        }
");

            builder.Append(@"
    }
}");

            context.AddSource("ConnectorGenerator", SourceText.From(builder.ToString(), Encoding.UTF8));
        }
    }
}
