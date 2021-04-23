using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FlightRecorder.Client.Logics
{
    public class RecorderLogic : IRecorderLogic
    {
        public event EventHandler<RecordsUpdatedEventArgs> RecordsUpdated;

        private readonly ILogger<RecorderLogic> logger;
        private readonly Stopwatch stopwatch = new();

        private long? startMilliseconds;
        private long? endMilliseconds;
        private List<(long milliseconds, AircraftPositionStruct position)> records = new();

        private bool IsStarted => startMilliseconds.HasValue && records != null;
        private bool IsEnded => startMilliseconds.HasValue && endMilliseconds.HasValue;

        public RecorderLogic(ILogger<RecorderLogic> logger)
        {
            this.logger = logger;
        }

        #region Public Functions

        public void Initialize()
        {
            logger.LogDebug("Initializing recorder...");

            stopwatch.Start();
        }

        public void Record()
        {
            logger.LogInformation("Start recording...");

            startMilliseconds = stopwatch.ElapsedMilliseconds;
            endMilliseconds = null;
            records = new List<(long milliseconds, AircraftPositionStruct position)>();
        }

        public void StopRecording()
        {
            endMilliseconds = stopwatch.ElapsedMilliseconds;
            logger.LogDebug("Recording stopped. {totalFrames} frames recorded.", records.Count);
        }

        public void NotifyPosition(AircraftPositionStruct? value)
        {
            if (IsStarted && !IsEnded && value.HasValue)
            {
                records.Add((stopwatch.ElapsedMilliseconds, value.Value));
                RecordsUpdated?.Invoke(this, new(records.Count));
            }
        }

        public SavedData ToData(string clientVersion)
            => new(clientVersion, startMilliseconds.Value, endMilliseconds.Value, records);

        #endregion
    }
}
