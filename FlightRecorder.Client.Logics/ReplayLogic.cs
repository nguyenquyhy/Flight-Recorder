using FlightRecorder.Client.SimConnectMSFS;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace FlightRecorder.Client.Logics
{
    public class ReplayLogic : IReplayLogic, IDisposable
    {
        public event EventHandler<RecordsUpdatedEventArgs>? RecordsUpdated;
        public event EventHandler? ReplayFinished;
        public event EventHandler<CurrentFrameChangedEventArgs>? CurrentFrameChanged;

        private const int EventThrottleMilliseconds = 500;

        private readonly ILogger<ReplayLogic> logger;
        private readonly IConnector connector;
        private readonly Stopwatch stopwatch = new();


        private long? startMilliseconds;
        private long? endMilliseconds;
        private SimStateStruct? startState;

        public List<(long milliseconds, AircraftPositionStruct position)> Records { get; private set; } = new();

        public string? AircraftTitle { get; set; }

        private int currentFrame;

        private double rate = 1;
        private int? pausedFrame;
        private double? pausedRate;
        private bool isReplayStopping;
        private long? replayMilliseconds;
        private long? pausedMilliseconds;
        private long? offsetStartMilliseconds;

        private AircraftPositionStruct? currentPosition = null;
        private long? lastTriggeredMilliseconds = null;
        private TaskCompletionSource<bool>? tcs;

        private bool IsReplayable => Records != null && Records.Count > 0;
        private bool IsReplaying => replayMilliseconds != null && pausedMilliseconds == null;
        private bool IsPausing => pausedMilliseconds != null;

        private bool IsAI([NotNullWhen(true)] string? aircraftTitle) => !string.IsNullOrEmpty(aircraftTitle);
        private uint? aiRequestId = null;
        private uint? aiId = null;
        // private Timer timer;

        public ReplayLogic(ILogger<ReplayLogic> logger, IConnector connector)
        {
            logger.LogDebug("Creating instance of {class}", nameof(ReplayLogic));

            this.logger = logger;
            this.connector = connector;

            RegisterEvents();
        }

        public void Dispose()
        {
            logger.LogDebug("Disposing {class}", nameof(RecorderLogic));
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                DeregisterEvents();
            }
        }

        private void RegisterEvents()
        {
            connector.AircraftIdReceived += Connector_AircraftIdReceived;
            connector.CreatingObjectFailed += Connector_CreatingObjectFailed;
            connector.Frame += Connector_Frame;
        }

        private void DeregisterEvents()
        {
            connector.AircraftIdReceived -= Connector_AircraftIdReceived;
            connector.CreatingObjectFailed -= Connector_CreatingObjectFailed;
            connector.Frame -= Connector_Frame;
        }

        #region Public Functions

        public bool Replay()
        {
            if (!IsReplayable)
            {
                logger.LogInformation("No record to replay!");
                return false;
            }

            logger.LogDebug("Initializing replay...");

            stopwatch.Start();

            logger.LogInformation("Start replay from {currentFrame}...", currentFrame);

            stopwatch.Restart();
            lastTriggeredMilliseconds = null;
            replayMilliseconds = stopwatch.ElapsedMilliseconds - offsetStartMilliseconds ?? 0;

            if (Records.Any())
            {
                var currentPosition = Records[currentFrame].position;
                if (IsAI(AircraftTitle))
                {
                    aiRequestId = connector.Spawn(AircraftTitle, currentPosition);
                }
                else
                {
                    connector.Init(0, currentPosition);
                }
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
                    if (pausedMilliseconds == null) throw new InvalidOperationException("Cannot resume without pause time!");
                    if (replayMilliseconds == null) throw new InvalidOperationException("Cannot resume without replay time!");
                    if (pausedRate == null) throw new InvalidOperationException("Cannot resume without pause rate!");
                    replayMilliseconds = stopwatch.ElapsedMilliseconds - (long)((pausedMilliseconds - replayMilliseconds) / rate * pausedRate);
                }
                else
                {
                    // Resume based on seeked frame
                    if (startMilliseconds == null) throw new InvalidOperationException("Cannot resume without start time!");
                    replayMilliseconds = stopwatch.ElapsedMilliseconds - (long)((Records[frame].milliseconds - startMilliseconds) / rate);
                }

                // Initialize resumed position
                if (frame == -1)
                {
                    // Ignore as this happens when Pause is clicked before the first frame is calculated
                }
                else if (frame == pausedFrame)
                {
                    // Ignore to prevent init unnecessarily
                }
                else if (frame >= 0 && frame < Records.Count)
                {
                    connector.Init(aiId ?? 0, Records[frame].position);
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
            else if (!IsReplaying)
            {
                (var elapsed, _) = Records[value];
                offsetStartMilliseconds = elapsed - startMilliseconds;
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
                if (!IsAI(AircraftTitle))
                {
                    connector.Unfreeze(0);
                }
                else if (aiId.HasValue)
                {
                    connector.Unfreeze(aiId.Value);
                }
            }
        }

        public void NotifyPosition(AircraftPositionStruct? value)
        {
            currentPosition = value;
        }

        public void FromData(string? fileName, SavedData data)
        {
            startMilliseconds = data.StartTime;
            endMilliseconds = data.EndTime;
            startState = data.StartState == null ? null : SimState.ToStruct(data.StartState);
            Records = data.Records.Select(r => (r.Time, AircraftPosition.ToStruct(r.Position))).ToList();
            RecordsUpdated?.Invoke(this, new(fileName, data.StartState?.AircraftTitle, Records.Count));
        }

        public SavedData ToData(string clientVersion)
        {
            if (startMilliseconds == null) throw new InvalidOperationException("Invalid replay data without start time!");
            if (endMilliseconds == null) throw new InvalidOperationException("Invalid replay data without end time!");
            return new(clientVersion, startMilliseconds.Value, endMilliseconds.Value, startState, Records);
        }

        #endregion

        #region Private Functions

        private void Connector_AircraftIdReceived(object? sender, AircraftIdReceivedEventArgs e)
        {
            if (IsAI(AircraftTitle) && aiRequestId == e.RequestId && aiId == null)
            {
                logger.LogDebug("Set AI ID {objectID}", e.ObjectId);
                aiRequestId = null;
                aiId = e.ObjectId;
            }
        }

        private void Connector_CreatingObjectFailed(object? sender, EventArgs e)
        {
            if (IsAI(AircraftTitle) && aiRequestId != null)
            {
                logger.LogDebug("Fail to spawn for request {requestID}", aiRequestId);
                aiRequestId = null;
                StopReplay();
            }
        }

        private void Connector_Frame(object? sender, EventArgs e)
        {
            Tick();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            //Tick();
        }

        private async Task RunReplay()
        {

            //timer = new Timer();
            //timer.Elapsed += Timer_Elapsed;
            //timer.Start();

            if (!IsAI(AircraftTitle))
            {
                connector.Freeze(0);
            }

            var enumerator = Records.GetEnumerator();
            currentFrame = -1;
            long? recordedElapsed = null;
            AircraftPositionStruct? position = null;

            long? lastElapsed = 0;
            AircraftPositionStruct? lastPosition = null;

            while (true)
            {
                // TODO: break this loop when window is closed

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

                if (IsAI(AircraftTitle) && aiId == null)
                {
                    // Wait for spawning
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

                            // Try to check the velocity
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
                    logger.LogTrace("Current Frame {currentFrame} {ellapsed}", currentFrame, currentElapsed);
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

            currentFrame = 0;
            isReplayStopping = false;

            if (IsAI(AircraftTitle))
            {
                //timer.Stop();
                //timer = null;

                if (aiId.HasValue)
                {
                    connector.Despawn(aiId.Value);
                    aiId = null;
                }
            }
            else
            {
                Unfreeze();
            }

            // Reset
            pausedMilliseconds = null;
            pausedFrame = null;
            replayMilliseconds = null;
            offsetStartMilliseconds = null;

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
            if (!IsAI(AircraftTitle) && currentPosition.HasValue && (lastTriggeredMilliseconds == null || stopwatch.ElapsedMilliseconds > lastTriggeredMilliseconds + EventThrottleMilliseconds))
            {
                lastTriggeredMilliseconds = stopwatch.ElapsedMilliseconds;
                connector.TriggerEvents(currentPosition.Value, position);
            }

            connector.Set(aiId ?? 0, nextValue);
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
