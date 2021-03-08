using System;
using System.Collections.Generic;

namespace FlightRecorder.Client.Logics
{
    public interface IRecorderLogic
    {
        List<(long milliseconds, AircraftPositionStruct position)> Records { get; }
        bool CanSave { get; }

        event EventHandler RecordsUpdated;
        event EventHandler ReplayFinished;
        event EventHandler<CurrentFrameChangedEventArgs> CurrentFrameChanged;

        void Initialize();
        void NotifyPosition(AircraftPositionStruct? value);
        void Record();
        void StopRecording();
        bool Replay();
        bool PauseReplay();
        bool ResumeReplay();
        void Seek(int value);
        bool StopReplay();
        void Tick();
        void ChangeRate(double rate);
        void Unfreeze();

        void FromData(SavedData data);
        SavedData ToData(string clientVersion);
    }
}