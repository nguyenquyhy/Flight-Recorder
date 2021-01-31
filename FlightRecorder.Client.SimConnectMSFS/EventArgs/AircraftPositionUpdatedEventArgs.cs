using System;

namespace FlightRecorder.Client.SimConnectMSFS
{
    public class AircraftPositionUpdatedEventArgs : EventArgs
    {
        public AircraftPositionUpdatedEventArgs(AircraftPositionStruct position)
        {
            Position = position;
        }

        public AircraftPositionStruct Position { get; }
    }
}
