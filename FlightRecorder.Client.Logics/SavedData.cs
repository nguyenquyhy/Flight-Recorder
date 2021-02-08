using System.Collections.Generic;
using System.Linq;

namespace FlightRecorder.Client.Logics
{
    public class SavedData
    {
        public SavedData()
        {

        }

        public SavedData(string clientVersion, long startTime, long endTime, List<(long milliseconds, AircraftPositionStruct position)> records)
        {
            ClientVersion = clientVersion;
            StartTime = startTime;
            EndTime = endTime;
            Records = records.Select(r => new SavedRecord
            {
                Time = r.milliseconds,
                Position = AircraftPosition.FromStruct(r.position)
            }).ToList();
        }

        public string ClientVersion { get; set; }
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public List<SavedRecord> Records { get; set; }

        public class SavedRecord
        {
            public long Time { get; set; }
            public AircraftPosition Position { get; set; }
        }
    }
}
