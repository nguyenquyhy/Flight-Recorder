using FlightRecorder.Client.Logics;
using FlightRecorder.Client.SimConnectMSFS;
using Microsoft.Extensions.Logging;
using System;
using static FlightRecorder.Client.StateMachine;

namespace FlightRecorder.Client
{
    public class Orchestrator
    {
        public StateMachine StateMachine { get; }
        public IThreadLogic ThreadLogic { get; }
        public IRecorderLogic RecorderLogic { get; }
        public IReplayLogic ReplayLogic { get; }
        public MainViewModel ViewModel { get; }

        public Orchestrator(ILogger<Orchestrator> logger,
            StateMachine stateMachine, IThreadLogic threadLogic, IRecorderLogic recorderLogic, IReplayLogic replayLogic, IConnector connector, MainViewModel viewModel)
        {
            logger.LogDebug("Creating instance of {class}", nameof(Orchestrator));

            StateMachine = stateMachine;
            ThreadLogic = threadLogic;
            RecorderLogic = recorderLogic;
            ReplayLogic = replayLogic;
            ViewModel = viewModel;
            replayLogic.ReplayFinished += ReplayFinished;

            connector.Initialized += Connector_Initialized;
            connector.Closed += Connector_Closed;
        }

        private async void Connector_Initialized(object sender, EventArgs e)
        {
            await StateMachine.TransitAsync(Event.Connect);
        }

        private async void Connector_Closed(object sender, EventArgs e)
        {
            await StateMachine.TransitAsync(Event.Disconnect);
        }

        private async void ReplayFinished(object sender, EventArgs e)
        {
            await StateMachine.TransitAsync(Event.Stop);
        }
    }
}
