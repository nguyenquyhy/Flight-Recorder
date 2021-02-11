using FlightRecorder.Client.SimConnectMSFS;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FlightRecorder.Client.Logics
{
    public class RecorderLogic
    {
        private const int EventThrottleMilliseconds = 500;

        public event EventHandler RecordsUpdated;
        public event EventHandler ReplayFinished;
        public event EventHandler<CurrentFrameChangedEventArgs> CurrentFrameChanged;

        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly ILogger<RecorderLogic> logger;
        private readonly Connector connector;

        private int currentFrame;
        public int CurrentFrame
        {
            get => currentFrame;
            set
            {
                if (IsPausing)
                {
                    logger.LogDebug("Slide changed to {value}", value);

                    currentFrame = value;
                    (var elapsed, var position) = Records[value];
                    MoveAircraft(elapsed, position, null, null, 0);
                }
                else
                {
                    throw new InvalidOperationException("Cannot set CurrentFrame when not pausing!");
                }
            }
        }

        private long? startMilliseconds;
        private long? endMilliseconds;
        private long? replayMilliseconds;
        private long? pausedMilliseconds;
        private int? pausedFrame;
        private AircraftPositionStruct? currentPosition = null;
        private long? lastTriggeredMilliseconds = null;

        private bool IsStarted => startMilliseconds.HasValue && Records != null;
        public bool IsEnded => startMilliseconds.HasValue && endMilliseconds.HasValue;
        public bool IsReplayable => Records != null && Records.Count > 0;
        public bool IsReplaying => replayMilliseconds != null && pausedMilliseconds == null;
        public bool IsPausing => pausedMilliseconds != null;

        public List<(long milliseconds, AircraftPositionStruct position)> Records { get; private set; }
        public AircraftPositionStruct? CurrentPosition
        {
            set
            {
                currentPosition = value;

                if (IsStarted && !IsEnded && value.HasValue)
                {
                    Records.Add((stopwatch.ElapsedMilliseconds, value.Value));
                    RecordsUpdated?.Invoke(this, new EventArgs());
                }
            }
        }

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

        public void Start()
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

                return true;
            }
            return false;
        }

        public bool ResumeReplay()
        {
            if (IsPausing)
            {
                if (currentFrame == pausedFrame)
                {
                    // No seeking => Resume based on pause time
                    replayMilliseconds += stopwatch.ElapsedMilliseconds - pausedMilliseconds;
                }
                else
                {
                    // Resume based on seeked frame
                    replayMilliseconds = stopwatch.ElapsedMilliseconds - (Records[currentFrame].milliseconds - startMilliseconds);
                }
                pausedMilliseconds = null;
                // NOTE: pausedFrame is not cleared here to allow resuming in the loop

                return true;
            }
            return false;
        }

        public bool StopReplay()
        {
            if (IsReplaying)
            {
                pausedMilliseconds = null;
                pausedFrame = null;
                replayMilliseconds = null;

                return true;
            }
            return false;
        }

        public SavedData ToData(string clientVersion)
            => new SavedData(clientVersion, startMilliseconds.Value, endMilliseconds.Value, Records);

        public void FromData(SavedData data)
        {
            startMilliseconds = data.StartTime;
            endMilliseconds = data.EndTime;
            Records = data.Records.Select(r => (r.Time, AircraftPosition.ToStruct(r.Position))).ToList();
            RecordsUpdated?.Invoke(this, new EventArgs());
        }

        #endregion

        #region Private Functions

        private void RunReplay()
        {
            connector.Pause();

            var enumerator = Records.GetEnumerator();
            currentFrame = 0;

            long? recordedElapsed = null;
            AircraftPositionStruct? position = null;

            long? lastElapsed = 0;
            AircraftPositionStruct? lastPosition = null;

            while (true)
            {
                var replayStartTime = replayMilliseconds;
                if (replayStartTime == null)
                {
                    FinishReplay();
                    return;
                }

                if (pausedMilliseconds == null)
                {
                    if (pausedFrame != null && pausedFrame != currentFrame)
                    {
                        // Reset the enumerator since user might seek backward
                        enumerator = Records.GetEnumerator();
                        currentFrame = 0;
                        recordedElapsed = null;
                        position = null;
                        pausedFrame = null;
                    }

                    var currentElapsed = stopwatch.ElapsedMilliseconds - replayStartTime.Value;

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

                Thread.Sleep(16);
            }
        }

        private void FinishReplay()
        {
            logger.LogInformation("Replay finished.");
            connector.Unpause();
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
                nextValue = nextValue * interpolation + AircraftPositionStructOperator.ToSet(lastPosition.Value) * (1 - interpolation);
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
