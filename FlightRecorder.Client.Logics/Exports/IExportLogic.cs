using System.Collections.Generic;
using System.Threading.Tasks;

namespace FlightRecorder.Client.Logics;

public interface IExportLogic
{
    Task ExportAsync(string fileName, IEnumerable<AircraftPosition> records);
    string GetFileFilter();
    string GetFileName();
}