using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace FlightRecorder.Client.Logics;

public class CsvExportLogic : IExportLogic
{
    public string GetFileName() => $"Export {DateTime.Now:yyyy-MM-dd-HH-mm}.csv";
    public string GetFileFilter() => "CSV (for Excel)|*.csv";

    public async Task ExportAsync(string fileName, IEnumerable<AircraftPosition> records)
    {
        using var writer = new StreamWriter(fileName);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        await csv.WriteRecordsAsync(records);
    }
}
