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
        private const int InitialEventID = 1000;

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
using Microsoft.Extensions.Logging;
using Microsoft.FlightSimulator.SimConnect;

namespace FlightRecorder.Client.SimConnectMSFS
{
    public partial class Connector
    {");
            builder.Append(@"
        private void RegisterSimStateDefinition()
        {
            RegisterDataDefinition<SimStateStruct>(DEFINITIONS.SimState");
            foreach ((_, _, var variable, var unit, var type, _, _, _, _) in simStateFields)
            {
                builder.Append($@",
                (""{variable}"", {(string.IsNullOrEmpty(unit) ? "null" : $"\"{unit}\"")}, (SIMCONNECT_DATATYPE){type})");
            }
            builder.Append(@"
            );
        }
");

            builder.Append(@"
        private void RegisterAircraftPositionDefinition()
        {
            RegisterDataDefinition<AircraftPositionStruct>(DEFINITIONS.AircraftPosition");
            foreach ((_, _, var variable, var unit, var type, _, _, _, _) in aircraftFields)
            {
                builder.Append($@",
                (""{variable}"", {(string.IsNullOrEmpty(unit) ? "null" : $"\"{unit}\"")}, (SIMCONNECT_DATATYPE){type})");
            }
            builder.Append(@"
            );
        }
");

            builder.Append(@"
        private void RegisterAircraftPositionSetDefinition()
        {
            RegisterDataDefinition<AircraftPositionSetStruct>(DEFINITIONS.AircraftPositionSet");
            foreach ((_, _, var variable, var unit, var type, var setType, _, _, _) in aircraftFields)
            {
                if (setType == null || setType == SetTypeDefault)
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
        private void RegisterEvents()
        {");
            var eventId = InitialEventID;
            foreach ((_, _, var variable, var unit, var type, var setType, var setBy, _, _) in aircraftFields)
            {
                if (setType == SetTypeEvent)
                {
                    // TODO: warning if setBy is empty
                    builder.Append($@"
            logger.LogDebug(""Register event {{eventName}} to ID {{eventID}}"", ""{setBy}"", {eventId});
            simconnect?.MapClientEventToSimEvent((EVENTS){eventId}, ""{setBy}"");");
                    eventId++;
                }
            }
            builder.Append(@"
        }
");
            builder.Append(@"
        public void TriggerEvents(AircraftPositionStruct current, AircraftPositionStruct expected)
        {");
            eventId = InitialEventID;
            foreach ((_, var name, var variable, var unit, var type, var setType, var setBy, _, _) in aircraftFields)
            {
                if (setType == SetTypeEvent)
                {
                    // TODO: warning if setBy is empty
                    builder.Append($@"
            if (current.{name} != expected.{name})
            {{
                logger.LogDebug(""Trigger event {{eventName}}"", ""{setBy}"");
                simconnect?.TransmitClientEvent(SimConnect.SIMCONNECT_OBJECT_ID_USER, (EVENTS){eventId}, 0, GROUPS.GENERIC, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
            }}
");
                    eventId++;
                }
            }
            builder.Append(@"
        }
");

            builder.Append(@"
    }
}");

            context.AddSource("ConnectorGenerator", SourceText.From(builder.ToString(), Encoding.UTF8));
        }
    }
}
