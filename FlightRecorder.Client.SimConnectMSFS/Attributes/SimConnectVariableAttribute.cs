using Microsoft.FlightSimulator.SimConnect;
using System;

namespace FlightRecorder.Client
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public class SimConnectVariableAttribute : Attribute
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public SIMCONNECT_DATATYPE Type { get; set; }
        public SetType SetType { get; set; }
        public string SetByEvent { get; set; }
    }

    public enum SetType
    {
        Default,
        Event,
        None,
    }
}
