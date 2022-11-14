using System.IO;
using System.Threading.Tasks;

namespace FlightRecorder.Client.Logics;

public interface IStorageLogic
{
    Task<SavedData?> LoadAsync(Stream file);
    Task SaveAsync(string filePath, SavedData data);
}
