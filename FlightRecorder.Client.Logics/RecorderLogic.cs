using FlightRecorder.Client.SimConnectMSFS;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FlightRecorder.Client.Logics;

public class RecorderLogic : IRecorderLogic, IDisposable
{
    public event EventHandler<RecordsUpdatedEventArgs>? RecordsUpdated;

    private readonly ILogger<RecorderLogic> logger;
    private readonly IConnector connector;
    private readonly Stopwatch stopwatch = new();

    private long? startMilliseconds;
    private long? endMilliseconds;
    private SimStateStruct startState;
    private List<(long milliseconds, AircraftPositionStruct position)> records = new();


    private SimStateStruct simState;

    private bool IsStarted => startMilliseconds.HasValue && records != null;
    private bool IsEnded => startMilliseconds.HasValue && endMilliseconds.HasValue;

    public RecorderLogic(ILogger<RecorderLogic> logger, IConnector connector)
    {
        logger.LogDebug("Creating instance of {class}", nameof(RecorderLogic));
        this.logger = logger;
        this.connector = connector;

        connector.SimStateUpdated += Connector_SimStateUpdated;
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
            connector.SimStateUpdated -= Connector_SimStateUpdated;
        }
    }

    private void Connector_SimStateUpdated(object? sender, SimStateUpdatedEventArgs e)
    {
        simState = e.State;
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
        startState = simState;
        records = new List<(long milliseconds, AircraftPositionStruct position)>();
    }

    public void StopRecording()
    {
        if (endMilliseconds == null)
        {
            endMilliseconds = stopwatch.ElapsedMilliseconds;
            logger.LogDebug("Recording stopped. {totalFrames} frames recorded.", records.Count);
        }
    }

    public void NotifyPosition(AircraftPositionStruct? value)
    {
        if (IsStarted && !IsEnded && value.HasValue)
        {
            records.Add((stopwatch.ElapsedMilliseconds, value.Value));
            RecordsUpdated?.Invoke(this, new(null, startState.AircraftTitle, records.Count));
        }
    }

    public SavedData ToData(string clientVersion)
    {
        if (startMilliseconds == null) throw new InvalidOperationException("Cannot get data before started recording!");
        if (endMilliseconds == null) throw new InvalidOperationException("Cannot get data before finished recording!");
        return new(clientVersion, startMilliseconds.Value, endMilliseconds.Value, startState, records);
    }

    #endregion
}
