using Microsoft.FlightSimulator.SimConnect;
using System;

namespace FlightRecorder.Client.SimConnectMSFS
{
    public class ConnectorErrorEventArgs : EventArgs
    {
        public ConnectorErrorEventArgs(SIMCONNECT_EXCEPTION error)
        {
            Error = error;
        }

        public SIMCONNECT_EXCEPTION Error { get; }

        public override string ToString()
        {
            return $"SimConnect error: {Error}";
        }
    }
}
