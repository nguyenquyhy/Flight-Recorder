using FlightRecorder.Client.SimConnectMSFS;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace FlightRecorder.Client.Logics
{
    public class RecorderLogic : IRecorderLogic
    {
        private const int EventThrottleMilliseconds = 500;

        public event EventHandler RecordsUpdated;
        public event EventHandler ReplayFinished;
        public event EventHandler<CurrentFrameChangedEventArgs> CurrentFrameChanged;

        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly ILogger<RecorderLogic> logger;
        private readonly Connector connector;

        private int currentFrame;

        private double rate = 1;
        private long? startMilliseconds;
        private long? endMilliseconds;
        private long? replayMilliseconds;
        private long? pausedMilliseconds;
        private int? pausedFrame;
        private double? pausedRate;
        private bool isReplayStopping;

        private AircraftPositionStruct? currentPosition = null;
        private long? lastTriggeredMilliseconds = null;
        private TaskCompletionSource<bool> tcs;

        public bool CanSave => IsEnded && IsReplayable;

        private bool IsStarted => startMilliseconds.HasValue && Records != null;
        private bool IsEnded => startMilliseconds.HasValue && endMilliseconds.HasValue;
        private bool IsReplayable => Records != null && Records.Count > 0;
        private bool IsReplaying => replayMilliseconds != null && pausedMilliseconds == null;
        private bool IsPausing => pausedMilliseconds != null;

        public List<(long milliseconds, AircraftPositionStruct position)> Records { get; private set; } = new();

        public RecorderLogic(ILogger<RecorderLogic> logger, Connector connector)
        {
            this.logger = logger;
            this.connector = connector;
        }

        #region Public Functions

        public void Initialize()
        {
            logger.LogDebug("Initializing recorder...");

            stopwatch.Start();
        }

        public void NotifyPosition(AircraftPositionStruct? value)
        {
            currentPosition = value;

            if (IsStarted && !IsEnded && value.HasValue)
            {
                Records.Add((stopwatch.ElapsedMilliseconds, value.Value));
                RecordsUpdated?.Invoke(this, new EventArgs());
            }
        }

        public void Record()
        {
            logger.LogInformation("Start recording...");

            startMilliseconds = stopwatch.ElapsedMilliseconds;
            endMilliseconds = null;
            Records = new List<(long milliseconds, AircraftPositionStruct position)>();
        }

        public void StopRecording()
        {
            endMilliseconds = stopwatch.ElapsedMilliseconds;
            logger.LogDebug("Recording stopped. {totalFrames} frames recorded.", Records.Count);
        }

        public bool Replay()
        {
            if (!IsReplayable)
            {
                logger.LogInformation("No record to replay!");
                return false;
            }

            logger.LogInformation("Start replay...");

            replayMilliseconds = stopwatch.ElapsedMilliseconds;

            if (Records.Any())
            {
                connector.Init(Records.First().position);
            }

            Task.Run(RunReplay);

            return true;
        }

        public bool PauseReplay()
        {
            if (IsReplaying)
            {
                logger.LogInformation("Pause recording...");

                pausedMilliseconds = stopwatch.ElapsedMilliseconds;
                pausedFrame = currentFrame;
                pausedRate = rate;

                return true;
            }
            return false;
        }

        /**** Timeline
         * replayMilliseconds
         *                                        pausedMilliseconds
         *                                                            stopwatch
         *                                                            resume
         */

        public bool ResumeReplay()
        {
            if (IsPausing)
            {
                var frame = currentFrame;
                if (frame == pausedFrame)
                {
                    // No seeking => Resume based on pause time
                    replayMilliseconds = stopwatch.ElapsedMilliseconds - (long)((pausedMilliseconds - replayMilliseconds) / rate * pausedRate);
                }
                else
                {
                    // Resume based on seeked frame
                    replayMilliseconds = stopwatch.ElapsedMilliseconds - (long)((Records[frame].milliseconds - startMilliseconds) / rate);
                }

                // Initialize resumed position
                if (frame >= 0 && frame < Records.Count)
                {
                    connector.Init(Records[frame].position);
                }
                else
                {
                    throw new InvalidOperationException($"Cannot resume at frame {frame} because there are only {Records.Count} frames!");
                }

                // Signal unpaused
                pausedMilliseconds = null;
                // NOTE: pausedFrame is not cleared here to allow resuming in the loop

                return true;
            }
            return false;
        }

        public bool StopReplay()
        {
            if (IsReplaying || IsPausing)
            {
                isReplayStopping = true;

                // Make sure at least one more tick happens to handle sim exit
                Tick();

                return true;
            }
            return false;
        }

        public void Seek(int value)
        {
            logger.LogTrace("Seek to {value}", value);

            currentFrame = value;

            if (IsPausing)
            {
                (var elapsed, var position) = Records[value];
                MoveAircraft(elapsed, position, null, null, 0);
            }
        }

        public void ChangeRate(double rate)
        {
            this.rate = rate;
        }

        public void Tick()
        {
            if (IsReplaying || IsPausing)
            {
                try
                {
                    tcs?.SetResult(true);
                }
                catch (InvalidOperationException ex)
                {
                    // Ignore since most likely tcs result is already set
                    logger.LogDebug(ex, "Cannot set TCS result on tick");
                }
            }
        }

        public void Unfreeze()
        {
            if (replayMilliseconds != null)
            {
                connector.Unpause();
            }
        }

        public SavedData ToData(string clientVersion)
            => new(clientVersion, startMilliseconds.Value, endMilliseconds.Value, Records);

        public void FromData(SavedData data)
        {
            startMilliseconds = data.StartTime;
            endMilliseconds = data.EndTime;
            Records = data.Records.Select(r => (r.Time, AircraftPosition.ToStruct(r.Position))).ToList();
            RecordsUpdated?.Invoke(this, new EventArgs());
        }

        #endregion

        #region Private Functions

        private async Task RunReplay()
        {
            connector.Pause();

            var enumerator = Records.GetEnumerator();
            currentFrame = -1;
            long? recordedElapsed = null;
            AircraftPositionStruct? position = null;

            long? lastElapsed = 0;
            AircraftPositionStruct? lastPosition = null;

            while (true)
            {
                // Wait for tick call from the sim frame
                tcs = new TaskCompletionSource<bool>();
                await tcs.Task;
                tcs = null;

                if (isReplayStopping)
                {
                    FinishReplay();
                    return;
                }

                var replayStartTime = replayMilliseconds;
                if (replayStartTime == null)
                {
                    // Safe-guard for Stopped
                    continue;
                }

                if (IsPausing)
                {
                    continue;
                }

                if (pausedFrame != null && pausedFrame != currentFrame)
                {
                    // Reset the enumerator since user might seek backward
                    logger.LogDebug("Reset interaction. Pause frame {frame}.", pausedFrame);

                    enumerator = Records.GetEnumerator();
                    currentFrame = -1;
                    recordedElapsed = null;
                    position = null;

                    pausedFrame = null;
                }

                var currentElapsed = (long)((stopwatch.ElapsedMilliseconds - replayStartTime.Value) * rate);

                try
                {
                    while (!recordedElapsed.HasValue || currentElapsed > recordedElapsed)
                    {
                        logger.LogTrace("Move next {currentElapsed}", currentElapsed);
                        var canMove = enumerator.MoveNext();

                        if (canMove)
                        {
                            currentFrame++;
                            (var recordedMilliseconds, var recordedPosition) = enumerator.Current;
                            lastElapsed = recordedElapsed;
                            lastPosition = position;
                            recordedElapsed = recordedMilliseconds - startMilliseconds;
                            position = recordedPosition;
                        }
                        else
                        {
                            // Last frame
                            FinishReplay();
                            return;
                        }
                    }
                }
                finally
                {
                    CurrentFrameChanged?.Invoke(this, new CurrentFrameChangedEventArgs(currentFrame));
                }

                if (position.HasValue && recordedElapsed.HasValue)
                {
                    MoveAircraft(recordedElapsed.Value, position.Value, lastElapsed, lastPosition, currentElapsed);
                }
            }
        }

        private void FinishReplay()
        {
            logger.LogInformation("Replay finished.");

            isReplayStopping = false;

            Unfreeze();

            // Reset
            pausedMilliseconds = null;
            pausedFrame = null;
            replayMilliseconds = null;

            ReplayFinished?.Invoke(this, new EventArgs());
        }

        private void MoveAircraft(long nextElapsed, AircraftPositionStruct position, long? lastElapsed, AircraftPositionStruct? lastPosition, long currentElapsed)
        {
            logger.LogTrace("Delta time {delta} {current} {recorded}.", currentElapsed - nextElapsed, currentElapsed, nextElapsed);

            var nextValue = AircraftPositionStructOperator.ToSet(position);
            if (lastPosition.HasValue && lastElapsed.HasValue)
            {
                var interpolation = (double)(currentElapsed - lastElapsed.Value) / (nextElapsed - lastElapsed.Value);
                if (interpolation == 0.5)
                {
                    // Edge case: let next value win so Math.round does not act unexpectedly
                    interpolation = 0.501;
                }
                nextValue = AircraftPositionStructOperator.Interpolate(nextValue, AircraftPositionStructOperator.ToSet(lastPosition.Value), interpolation);
            }
            if (currentPosition.HasValue && (lastTriggeredMilliseconds == null || stopwatch.ElapsedMilliseconds > lastTriggeredMilliseconds + EventThrottleMilliseconds))
            {
                lastTriggeredMilliseconds = stopwatch.ElapsedMilliseconds;
                connector.TriggerEvents(currentPosition.Value, position);
            }

            connector.Set(nextValue);
        }

        #endregion
    }
}
