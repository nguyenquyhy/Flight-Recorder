using System.IO;
using System.Threading.Tasks;

namespace FlightRecorder.Client.Logics;

public interface IDialogLogic
{
    bool Confirm(string message);
    void Error(string error);
    Task<string?> PickSaveFileAsync();
    Task<(string filePath, Stream fileStream)?> PickOpenFileAsync();
    Task<string?> PickSaveFolderAsync();
}
