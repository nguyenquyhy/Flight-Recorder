using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace FlightRecorder.Client.Logics;

public class SavedData
{

    public SavedData(string clientVersion, long startTime, long endTime, SimStateStruct? simState, List<(long milliseconds, AircraftPositionStruct position)> records)
    {
        ClientVersion = clientVersion;
        StartTime = startTime;
        EndTime = endTime;
        StartState = simState.HasValue ? SimState.FromStruct(simState.Value) : null;
        Records = records.Select(r => new SavedRecord
        (
            r.milliseconds,
            AircraftPosition.FromStruct(r.position)
        )).ToList();
    }

    [JsonConstructor]
    public SavedData(string clientVersion, long startTime, long endTime, SimState? startState, List<SavedRecord>? records)
    {
        ClientVersion = clientVersion;
        StartTime = startTime;
        EndTime = endTime;
        StartState = startState;
        Records = records ?? new List<SavedRecord>();
    }

    public string ClientVersion { get; set; }
    public long StartTime { get; set; }
    public long EndTime { get; set; }
    public SimState? StartState { get; set; }
    public List<SavedRecord> Records { get; set; }

    public class SavedRecord
    {
        public SavedRecord(long time, AircraftPosition position)
        {
            Time = time;
            Position = position;
        }

        public long Time { get; set; }
        public AircraftPosition Position { get; set; }
    }
}
