using FlightRecorder.Client.Logics;

namespace FlightRecorder.Client
{
    public interface IDialogLogic
    {
        bool Confirm(string message);
        void Error(string error);
        SavedData Load();
    }
}
