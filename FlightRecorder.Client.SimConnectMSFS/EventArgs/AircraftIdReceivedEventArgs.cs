using System;

namespace FlightRecorder.Client.SimConnectMSFS
{
    public class AircraftIdReceivedEventArgs : EventArgs
    {
        public AircraftIdReceivedEventArgs(uint requestId, uint objectId)
        {
            RequestId = requestId;
            ObjectId = objectId;
        }

        public uint RequestId { get; }
        public uint ObjectId { get; }
    }
}
