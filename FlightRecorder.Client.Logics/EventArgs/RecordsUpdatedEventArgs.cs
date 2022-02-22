using System;

namespace FlightRecorder.Client.Logics
{
    public class RecordsUpdatedEventArgs : EventArgs
    {
        public string? FileName { get; }
        public string? AircraftTitle { get; }
        public int RecordCount { get; }

        public RecordsUpdatedEventArgs(string? fileName, string? aircraftTitle, int recordCount)
        {
            FileName = fileName;
            AircraftTitle = aircraftTitle;
            RecordCount = recordCount;
        }
    }
}
