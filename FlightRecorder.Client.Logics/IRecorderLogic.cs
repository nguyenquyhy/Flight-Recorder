using System;

namespace FlightRecorder.Client.Logics
{
    public interface IRecorderLogic
    {
        event EventHandler<RecordsUpdatedEventArgs> RecordsUpdated;

        void Initialize();
        void Record();
        void StopRecording();
        void NotifyPosition(AircraftPositionStruct? value);

        SavedData ToData(string clientVersion);
    }
}