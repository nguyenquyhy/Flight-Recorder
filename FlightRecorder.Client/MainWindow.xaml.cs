using FlightRecorder.Client.Logics;
using FlightRecorder.Client.SimConnectMSFS;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace FlightRecorder.Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : BaseWindow
    {
        private readonly ILogger<MainWindow> logger;
        private readonly IConnector connector;
        private readonly ICrashLogic crashLogic;
        private readonly DrawingLogic drawingLogic;
        private readonly ExportLogic exportLogic;
        private readonly WindowFactory windowFactory;
        private readonly IRecorderLogic recorderLogic;

        private readonly string currentVersion;

        private IntPtr Handle;

        public MainWindow(ILogger<MainWindow> logger,
            IConnector connector,
            ICrashLogic crashLogic,
            DrawingLogic drawingLogic,
            ExportLogic exportLogic,
            VersionLogic versionLogic,
            Orchestrator orchestrator,
            WindowFactory windowFactory)
            : base(orchestrator.ThreadLogic, orchestrator.StateMachine, orchestrator.ViewModel, orchestrator.ReplayLogic)
        {
            InitializeComponent();

            this.logger = logger;
            this.connector = connector;
            this.crashLogic = crashLogic;
            this.drawingLogic = drawingLogic;
            this.exportLogic = exportLogic;
            this.windowFactory = windowFactory;
            this.recorderLogic = orchestrator.RecorderLogic;

            stateMachine.StateChanged += StateMachine_StateChanged;

            connector.AircraftPositionUpdated += Connector_AircraftPositionUpdated;
            connector.Closed += Connector_Closed;

            DataContext = viewModel;

            currentVersion = versionLogic.GetVersion();
            Title += " " + currentVersion;
        }

        private void StateMachine_StateChanged(object? sender, StateChangedEventArgs e)
        {
            if (e.By == StateMachine.Event.Stop)
            {
                // Stop recording
                drawingLogic.ClearCache();
                Draw();
            }
        }

        protected async override Task Window_LoadedAsync(object sender, RoutedEventArgs e)
        {
            await base.Window_LoadedAsync(sender, e);

            await crashLogic.LoadDataAsync(stateMachine, replayLogic);
            
            // Create an event handle for the WPF window to listen for SimConnect events
            Handle = new WindowInteropHelper(sender as Window).Handle; // Get handle of main WPF Window
            var HandleSource = HwndSource.FromHwnd(Handle); // Get source of handle in order to add event handlers to it
            HandleSource.AddHook(HandleHook);
            InitializeConnector();
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (stateMachine.CurrentState != StateMachine.State.End // Already exiting
                && stateMachine.CurrentState != StateMachine.State.Start // Most likely due to single instance enforcement
                )
            {
                e.Cancel = true;
                if (await stateMachine.TransitAsync(StateMachine.Event.Exit))
                {
                    if (stateMachine.CurrentState == StateMachine.State.End)
                    {
                        Application.Current?.Shutdown();
                    }
                }
            }
        }

        private IntPtr HandleHook(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam, ref bool isHandled)
        {
            try
            {
                connector.HandleSimConnectEvents(message, ref isHandled);
                return IntPtr.Zero;
            }
            catch (BadImageFormatException)
            {
                return IntPtr.Zero;
            }
        }

        private void Connector_AircraftPositionUpdated(object? sender, AircraftPositionUpdatedEventArgs e)
        {
            recorderLogic.NotifyPosition(e.Position);
            replayLogic.NotifyPosition(e.Position);

            Dispatcher.Invoke(() =>
            {
                viewModel.AircraftPosition = AircraftPosition.FromStruct(e.Position);
            });
        }

        private void Connector_Closed(object? sender, EventArgs e)
        {
            logger.LogDebug("Start reconnecting...");
            InitializeConnector();
        }

        private async void ButtonRecord_Click(object sender, RoutedEventArgs e)
        {
            await stateMachine.TransitAsync(StateMachine.Event.Record);
        }

        private async void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            await stateMachine.TransitAsync(StateMachine.Event.Stop);
        }

        private void ButtonReplayAI_Click(object sender, RoutedEventArgs e)
        {
            var window = CreateAIWindow();
            window.Owner = this;
            window.ShowInTaskbar = false;
            window.ShowWithData(viewModel.SimState?.AircraftTitle, viewModel.FileName, replayLogic.ToData(currentVersion));
        }

        private async void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            await stateMachine.TransitAsync(StateMachine.Event.Save);
        }

        private async void ButtonExport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                FileName = $"Export {DateTime.Now:yyyy-MM-dd-HH-mm}.csv",
                Filter = "CSV (for Excel)|*.csv"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await exportLogic.ExportAsync(dialog.FileName, replayLogic.Records.Select(o =>
                    {
                        var result = AircraftPosition.FromStruct(o.position);
                        result.Milliseconds = o.milliseconds;
                        return result;
                    }));

                    logger.LogDebug("Save file into {fileName}", dialog.FileName);
                }
                catch (IOException)
                {
                    MessageBox.Show("Flight Recorder cannot write the file to disk.\nPlease make sure the folder is accessible by Flight Recorder, and you are not overwriting a locked file.", "Flight Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ButtonLoad_Click(object sender, RoutedEventArgs e)
        {
            if (await stateMachine.TransitAsync(StateMachine.Event.Load))
            {
                drawingLogic.ClearCache();
                Draw();
            }
        }

        private void ButtonLoadAI_Click(object sender, RoutedEventArgs e)
        {
            var window = CreateAIWindow();
            window.Owner = this;
            window.ShowInTaskbar = false;
            window.ShowWithData(viewModel.SimState?.AircraftTitle);
        }

        private void ButtonShowData_Click(object sender, RoutedEventArgs e)
        {
            viewModel.ShowData = !viewModel.ShowData;
            Height = viewModel.ShowData ? 472 : 307;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as MenuItem)?.Header is string header && double.TryParse(header[1..], NumberStyles.Any, CultureInfo.InvariantCulture, out var rate))
            {
                ButtonSpeed.Content = header;
                replayLogic.ChangeRate(rate);
            }
        }

        private void ToggleButtonTopmost_Checked(object sender, RoutedEventArgs e)
        {
            Topmost = true;
        }

        private void ToggleButtonTopmost_Unchecked(object sender, RoutedEventArgs e)
        {
            Topmost = false;
        }

        #region Single Instance

        public void RestoreWindow()
        {
            WindowState = WindowState.Normal;
            Activate();
        }

        #endregion

        private void InitializeConnector()
        {
            Dispatcher.Invoke(async () =>
            {
                while (true)
                {
                    try
                    {
                        connector.Initialize(Handle);
                        break;
                    }
                    catch (BadImageFormatException)
                    {
                        MessageBox.Show("Cannot initialize SimConnect!");
                        viewModel.SimConnectState = SimConnectState.Failed;
                        break;
                    }
                    catch (COMException ex)
                    {
                        logger.LogTrace(ex, "SimConnect error.");
                        viewModel.SimConnectState = SimConnectState.NotConnected;
                        await Task.Delay(5000).ConfigureAwait(true);
                    }
                }
            });
        }

        protected override void Draw()
        {
            drawingLogic.Draw(replayLogic.Records, () => viewModel.CurrentFrame, viewModel.State, (int)ImageWrapper.ActualWidth, (int)ImageWrapper.ActualHeight, ImageChart);
        }

        private void TextBlock_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                Clipboard.SetText(textBlock.Text);
            }
        }

        private AIWindow CreateAIWindow()
        {
            var serviceProvider = (Application.Current as App)?.ServiceProvider ??
                            throw new InvalidOperationException("ServiceProvider is not initialized!");
            var window = windowFactory.Create<AIWindow>(serviceProvider);
            return window;
        }
    }
}
