using System;
using System.Collections.Generic;

namespace FlightRecorder.Client.Logics
{
    public interface IReplayLogic
    {
        event EventHandler<RecordsUpdatedEventArgs> RecordsUpdated;
        event EventHandler ReplayFinished;
        event EventHandler<CurrentFrameChangedEventArgs> CurrentFrameChanged;

        List<(long milliseconds, AircraftPositionStruct position)> Records { get; }
        string? AircraftTitle { get; set; }

        bool Replay();
        bool PauseReplay();
        bool ResumeReplay();
        void Seek(int value);
        bool StopReplay();
        void ChangeRate(double rate);
        void Unfreeze();
        void NotifyPosition(AircraftPositionStruct? value);

        void FromData(string fileName, SavedData data);
        SavedData ToData(string clientVersion);
    }
}
