using FlightRecorder.Client.Logics;
using FlightRecorder.Client.SimConnectMSFS;
using Microsoft.Extensions.Logging;
using System;
using static FlightRecorder.Client.StateMachine;

namespace FlightRecorder.Client;

public class Orchestrator : IDisposable
{
    public IStateMachine StateMachine { get; }
    public IThreadLogic ThreadLogic { get; }
    public IRecorderLogic RecorderLogic { get; }
    public IReplayLogic ReplayLogic { get; }
    public MainViewModel ViewModel { get; }

    private readonly ILogger<Orchestrator> logger;
    private readonly IConnector connector;

    public Orchestrator(ILogger<Orchestrator> logger,
        IStateMachine stateMachine, IThreadLogic threadLogic, IRecorderLogic recorderLogic, IReplayLogic replayLogic, IConnector connector, 
        MainViewModel viewModel)
    {
        logger.LogDebug("Creating instance of {class}", nameof(Orchestrator));
        StateMachine = stateMachine;
        ThreadLogic = threadLogic;
        RecorderLogic = recorderLogic;
        ReplayLogic = replayLogic;
        ViewModel = viewModel;

        this.logger = logger;
        this.connector = connector;
        RegisterEvents(connector);
    }

    public void Dispose()
    {
        logger.LogDebug("Disposing {class}", nameof(Orchestrator));
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

    private void RegisterEvents(IConnector connector)
    {
        ReplayLogic.ReplayFinished += ReplayFinished;
        connector.Initialized += Connector_Initialized;
        connector.Closed += Connector_Closed;
    }

    private void DeregisterEvents()
    {
        ReplayLogic.ReplayFinished -= ReplayFinished;
        connector.Initialized -= Connector_Initialized;
        connector.Closed -= Connector_Closed;
    }

    private async void Connector_Initialized(object? sender, EventArgs e)
    {
        await StateMachine.TransitAsync(Event.Connect);
    }

    private async void Connector_Closed(object? sender, EventArgs e)
    {
        await StateMachine.TransitAsync(Event.Disconnect);
    }

    private async void ReplayFinished(object? sender, EventArgs e)
    {
        await StateMachine.TransitAsync(Event.Stop);
    }
}
