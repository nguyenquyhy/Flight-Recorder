using FlightRecorder.Client.SimConnectMSFS;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace FlightRecorder.Client.Logics
{
    public class ReplayLogic : IReplayLogic
    {
        public event EventHandler<RecordsUpdatedEventArgs> RecordsUpdated;
        public event EventHandler ReplayFinished;
        public event EventHandler<CurrentFrameChangedEventArgs> CurrentFrameChanged;

        private const int EventThrottleMilliseconds = 500;

        private readonly ILogger<ReplayLogic> logger;
        private readonly Connector connector;
        private readonly Stopwatch stopwatch = new();


        private long? startMilliseconds;
        private long? endMilliseconds;

        public List<(long milliseconds, AircraftPositionStruct position)> Records { get; private set; } = new();

        private int currentFrame;

        private double rate = 1;
        private int? pausedFrame;
        private double? pausedRate;
        private bool isReplayStopping;
        private long? replayMilliseconds;
        private long? pausedMilliseconds;

        private AircraftPositionStruct? currentPosition = null;
        private long? lastTriggeredMilliseconds = null;
        private TaskCompletionSource<bool> tcs;

        private bool IsReplayable => Records != null && Records.Count > 0;
        private bool IsReplaying => replayMilliseconds != null && pausedMilliseconds == null;
        private bool IsPausing => pausedMilliseconds != null;

        public ReplayLogic(ILogger<ReplayLogic> logger, Connector connector)
        {
            this.logger = logger;
            this.connector = connector;

            connector.Frame += Connector_Frame;
        }

        #region Public Functions

        public bool Replay()
        {
            if (!IsReplayable)
            {
                logger.LogInformation("No record to replay!");
                return false;
            }

            logger.LogInformation("Start replay...");

            stopwatch.Restart();
            lastTriggeredMilliseconds = null;
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
                if (frame == -1)
                {
                    // Ignore as this happens when Pause is clicked before the first frame is calculated
                }
                else if (frame >= 0 && frame < Records.Count)
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

        public void Unfreeze()
        {
            if (replayMilliseconds != null)
            {
                connector.Unpause();
            }
        }

        public void NotifyPosition(AircraftPositionStruct? value)
        {
            currentPosition = value;
        }

        public void FromData(SavedData data)
        {
            startMilliseconds = data.StartTime;
            endMilliseconds = data.EndTime;
            Records = data.Records.Select(r => (r.Time, AircraftPosition.ToStruct(r.Position))).ToList();
            RecordsUpdated?.Invoke(this, new(Records.Count));
        }

        public SavedData ToData(string clientVersion)
            => new(clientVersion, startMilliseconds.Value, endMilliseconds.Value, Records);

        #endregion

        #region Private Functions

        private void Connector_Frame(object sender, EventArgs e)
        {
            Tick();
        }

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

        private void Tick()
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

        #endregion
    }
}
