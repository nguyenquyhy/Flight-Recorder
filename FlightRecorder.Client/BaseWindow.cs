using FlightRecorder.Client.Logics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FlightRecorder.Client
{
    public abstract class BaseWindow : Window
    {
        protected readonly IThreadLogic threadLogic;
        protected readonly StateMachine stateMachine;
        protected readonly MainViewModel viewModel;
        protected readonly IReplayLogic replayLogic;

        public BaseWindow(IThreadLogic threadLogic, StateMachine stateMachine, MainViewModel viewModel, IReplayLogic replayLogic)
        {
            this.threadLogic = threadLogic;
            this.stateMachine = stateMachine;
            this.viewModel = viewModel;
            this.replayLogic = replayLogic;

            Loaded += BaseWindow_Loaded;
            SizeChanged += BaseWindow_SizeChanged;
        }

        private async void BaseWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Window_LoadedAsync(sender, e);
        }

        private void BaseWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Draw();
        }

        protected virtual async Task Window_LoadedAsync(object sender, RoutedEventArgs e)
        {
            (threadLogic as ThreadLogic).Register(this);
            await stateMachine.TransitAsync(StateMachine.Event.StartUp);

            viewModel.SimConnectState = SimConnectState.Connecting;
        }

        protected abstract void Draw();

        #region Common Event Handlers

        protected void ButtonContext_Click(object sender, RoutedEventArgs e)
        {
            (sender as Button).ContextMenu.IsOpen = true;
        }

        protected async void ButtonReplay_Click(object sender, RoutedEventArgs e)
        {
            await stateMachine.TransitAsync(StateMachine.Event.Replay);
        }

        protected async void ButtonPauseReplay_Click(object sender, RoutedEventArgs e)
        {
            await stateMachine.TransitAsync(StateMachine.Event.Pause);
        }

        protected async void ButtonResumeReplay_Click(object sender, RoutedEventArgs e)
        {
            await stateMachine.TransitAsync(StateMachine.Event.Resume);
        }

        protected async void ButtonStopReplay_Click(object sender, RoutedEventArgs e)
        {
            await stateMachine.TransitAsync(StateMachine.Event.RequestStopping);
        }

        protected void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            replayLogic.Seek((int)e.NewValue);
            Draw();
        }

        protected void Slider_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var currentFrame = viewModel.CurrentFrame;
            switch (e.Delta)
            {
                case > 0:
                    if (currentFrame > 0)
                    {
                        viewModel.CurrentFrame = currentFrame - 1;
                    }
                    break;
                case < 0:
                    if (currentFrame < viewModel.FrameCount - 1)
                    {
                        viewModel.CurrentFrame = currentFrame + 1;
                    }
                    break;
            }
        }

        #endregion
    }
}
