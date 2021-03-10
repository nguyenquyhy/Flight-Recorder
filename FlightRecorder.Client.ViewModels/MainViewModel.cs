using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlightRecorder.Client
{

    public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
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

    public class MainViewModel : BaseViewModel
    {
        public bool IsThrottlingChart => State == StateMachine.State.Recording || State == StateMachine.State.ReplayingSaved || State == StateMachine.State.ReplayingUnsaved;

        private SimConnectState simConnectState;
        public SimConnectState SimConnectState { get => simConnectState; set => SetProperty(ref simConnectState, value); }

        private AircraftPosition aircraftPosition;
        public AircraftPosition AircraftPosition { get => aircraftPosition; set => SetProperty(ref aircraftPosition, value); }

        private StateMachine.State state = StateMachine.State.Start;
        public StateMachine.State State { get => state; set => SetProperty(ref state, value); }

        private int frameCount;
        public int FrameCount { get => frameCount; set => SetProperty(ref frameCount, value); }

        private int currentFrame;
        public int CurrentFrame { get => currentFrame; set => SetProperty(ref currentFrame, value); }

        private bool showData;
        public bool ShowData { get => showData; set => SetProperty(ref showData, value); }
    }
}
