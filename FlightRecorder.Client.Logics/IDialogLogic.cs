using System.Threading.Tasks;

namespace FlightRecorder.Client.Logics;

public interface IDialogLogic
{
    bool Confirm(string message);
    void Error(string error);
    Task<string?> SaveAsync(SavedData data);
    Task<(string? fileName, SavedData? data)> LoadAsync();
}
