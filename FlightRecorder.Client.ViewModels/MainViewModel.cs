using FlightRecorder.Client.Logics;
using FlightRecorder.Client.SimConnectMSFS;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FlightRecorder.Client
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if ((storage == null && value != null) || (storage != null && !storage.Equals(value)))
            {
                storage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                return true;
            }
            return false;
        }
    }

    public enum SimConnectState
    {
        NotConnected,
        Connecting,
        Connected,
        Failed
    }

    public class MainViewModel : BaseViewModel, IDisposable
    {
        private readonly ILogger<MainViewModel> logger;
        private readonly IThreadLogic threadLogic;
        private readonly IRecorderLogic recorderLogic;
        private readonly IReplayLogic replayLogic;
        private readonly IConnector connector;

        public MainViewModel(ILogger<MainViewModel> logger, IThreadLogic threadLogic, IRecorderLogic recorderLogic, IReplayLogic replayLogic, IConnector connector)
        {
            logger.LogDebug("Creating instance of {class}", nameof(MainViewModel));

            this.logger = logger;
            this.threadLogic = threadLogic;
            this.recorderLogic = recorderLogic;
            this.replayLogic = replayLogic;
            this.connector = connector;

            RegisterEvents();
        }

        public void Dispose()
        {
            logger.LogDebug("Disposing {class}", nameof(MainViewModel));
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
            recorderLogic.RecordsUpdated += RecordsUpdated;
            replayLogic.RecordsUpdated += RecordsUpdated;
            replayLogic.CurrentFrameChanged += CurrentFrameChanged;
            connector.SimStateUpdated += SimStateUpdated;
        }

        private void DeregisterEvents()
        {
            recorderLogic.RecordsUpdated -= RecordsUpdated;
            replayLogic.RecordsUpdated -= RecordsUpdated;
            replayLogic.CurrentFrameChanged -= CurrentFrameChanged;
            connector.SimStateUpdated -= SimStateUpdated;
        }

        private void RecordsUpdated(object? sender, RecordsUpdatedEventArgs e)
        {
            threadLogic.RunInUIThread(() =>
            {
                FileName = e.FileName;
                AircraftTitle = e.AircraftTitle;
                FrameCount = e.RecordCount;
            });
        }

        private void CurrentFrameChanged(object? sender, CurrentFrameChangedEventArgs e)
        {
            try
            {
                threadLogic.RunInUIThread(() =>
                {
                    CurrentFrame = e.CurrentFrame;
                });
            }
            catch (TaskCanceledException)
            {
                // Exiting
            }
        }

        private void SimStateUpdated(object? sender, SimStateUpdatedEventArgs e)
        {
            threadLogic.RunInUIThread(() =>
            {
                SimState = SimState.FromStruct(e.State);
            });
        }

        private SimConnectState simConnectState;
        public SimConnectState SimConnectState { get => simConnectState; set => SetProperty(ref simConnectState, value); }

        private SimState? simState;
        public SimState? SimState { get => simState; set => SetProperty(ref simState, value); }

        private AircraftPosition? aircraftPosition;
        public AircraftPosition? AircraftPosition { get => aircraftPosition; set => SetProperty(ref aircraftPosition, value); }

        private StateMachine.State state = StateMachine.State.Start;
        public StateMachine.State State { get => state; set => SetProperty(ref state, value); }

        private int frameCount;
        public int FrameCount { get => frameCount; set => SetProperty(ref frameCount, value); }

        private int currentFrame;
        public int CurrentFrame { get => currentFrame; set => SetProperty(ref currentFrame, value); }

        private string? replayAircraftTitle;
        public string? ReplayAircraftTitle { get => replayAircraftTitle; set => SetProperty(ref replayAircraftTitle, value); }

        private string? currentAircraftTitle;
        public string? CurrentAircraftTitle { get => currentAircraftTitle; set => SetProperty(ref currentAircraftTitle, value); }

        private string? aircraftTitle;
        public string? AircraftTitle { get => aircraftTitle; set => SetProperty(ref aircraftTitle, value); }

        private string? fileName;
        public string? FileName { get => fileName; set => SetProperty(ref fileName, value); }

        private bool showData;
        public bool ShowData { get => showData; set => SetProperty(ref showData, value); }
    }
}
