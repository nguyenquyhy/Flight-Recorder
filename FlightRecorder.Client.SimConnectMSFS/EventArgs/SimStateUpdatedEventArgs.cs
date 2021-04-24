using System;

namespace FlightRecorder.Client.SimConnectMSFS
{
    public class SimStateUpdatedEventArgs : EventArgs
    {
        public SimStateUpdatedEventArgs(SimStateStruct state)
        {
            State = state;
        }

        public SimStateStruct State { get; }
    }
}
