using FlightRecorder.Client.Logics;
using System.Threading.Tasks;

namespace FlightRecorder.Client
{
    public interface IDialogLogic
    {
        bool Confirm(string message);
        void Error(string error);
        Task<bool> SaveAsync(SavedData data);
        Task<SavedData> LoadAsync();
    }
}
