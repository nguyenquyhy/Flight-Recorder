using FlightRecorder.Client.Logics;
using FlightRecorder.Client.SimConnectMSFS;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FlightRecorder.Client
{
    /// <summary>
    /// Interaction logic for AIWindow.xaml
    /// </summary>
    public partial class AIWindow : BaseWindow
    {
        private readonly IConnector connector;
        private readonly DrawingLogic drawingLogic;

        public AIWindow(Orchestrator orchestrator, IConnector connector, DrawingLogic drawingLogic)
            : base(orchestrator.ThreadLogic, orchestrator.StateMachine, orchestrator.ViewModel, orchestrator.ReplayLogic)
        {
            InitializeComponent();

            DataContext = orchestrator.ViewModel;
            this.connector = connector;
            this.drawingLogic = drawingLogic;
        }

        public void ShowWithData(string currentAircraftTitle, string fileName, SavedData savedData)
        {
            viewModel.CurrentAircraftTitle = currentAircraftTitle;
            replayLogic.FromData(fileName, savedData);
            replayLogic.AircraftTitle = viewModel.AircraftTitle;
            Show();
        }
        public void ShowWithData(string currentAircraftTitle)
        {
            viewModel.CurrentAircraftTitle = currentAircraftTitle;
            Show();
        }

        protected override async Task Window_LoadedAsync(object sender, RoutedEventArgs e)
        {
            await base.Window_LoadedAsync(sender, e);

            if (connector.IsInitialized)
            {
                await stateMachine.TransitAsync(StateMachine.Event.Connect);
            }

            var loaded = true;
            if (replayLogic.Records.Count == 0)
            {
                loaded = await LoadAsync();
            }
            else
            {
                await stateMachine.TransitAsync(StateMachine.Event.LoadAI);
            }

            if (loaded)
            {
                viewModel.ReplayAircraftTitle = viewModel.AircraftTitle;
                AskForAircraftTitle();
            }
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (stateMachine.CurrentState != StateMachine.State.End // Already exiting
                )
            {
                e.Cancel = true;
                if (await stateMachine.TransitAsync(StateMachine.Event.Exit))
                {
                    if (stateMachine.CurrentState == StateMachine.State.End)
                    {
                        // At this point, this might still be in the same thread because there is no async operation triggered.
                        // So we want to force a thread switch to be able to Close again.
                        await Task.Delay(10);
                        Close();
                        Owner.Activate();
                    }
                }
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as MenuItem).Header is string header && double.TryParse(header[1..], NumberStyles.Any, CultureInfo.InvariantCulture, out var rate))
            {
                ButtonSpeed.Content = header;
                replayLogic.ChangeRate(rate);
            }
        }

        private void ButtonChange_Click(object sender, RoutedEventArgs e)
        {
            AskForAircraftTitle(true);
        }

        private async Task<bool> LoadAsync()
        {
            if (await stateMachine.TransitAsync(StateMachine.Event.Load))
            {
                Draw();
                return true;
            }
            else
            {
                Close();
                return false;
            }
        }

        private void AskForAircraftTitle(bool force = false)
        {
            if (force || string.IsNullOrEmpty(viewModel.ReplayAircraftTitle))
            {
                var dialog = new AircraftNameDialog(viewModel);
                dialog.Owner = this;

                if (dialog.ShowDialog() == true)
                {
                    viewModel.ReplayAircraftTitle = dialog.TextName.Text.Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(viewModel.ReplayAircraftTitle))
            {
                Close();
            }

            replayLogic.AircraftTitle = viewModel.ReplayAircraftTitle;
        }

        protected override void Draw()
        {
            drawingLogic.Draw(replayLogic.Records, () => viewModel.CurrentFrame, viewModel.State, (int)ImageWrapper.ActualWidth, (int)ImageWrapper.ActualHeight, ImageChart);
        }
    }
}
