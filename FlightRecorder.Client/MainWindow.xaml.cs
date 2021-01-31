using FlightRecorder.Client.SimConnectMSFS;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace FlightRecorder.Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow> logger;
        private readonly MainViewModel viewModel;
        private readonly Connector connector;
        private readonly Stopwatch stopwatch = new Stopwatch();

        private IntPtr Handle;

        private long? startMilliseconds;
        private long? endMilliseconds;
        private long? replayMilliseconds;
        private List<(long milliseconds, AircraftPositionStruct position)> records = null;

        public MainWindow(ILogger<MainWindow> logger, MainViewModel viewModel, Connector connector)
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.logger = logger;
            this.viewModel = viewModel;
            this.connector = connector;

            connector.AircraftPositionUpdated += Connector_AircraftPositionUpdated;

            DataContext = viewModel;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                viewModel.SimConnectState = SimConnectState.Connecting;

                // Create an event handle for the WPF window to listen for SimConnect events
                Handle = new WindowInteropHelper(sender as Window).Handle; // Get handle of main WPF Window
                var HandleSource = HwndSource.FromHwnd(Handle); // Get source of handle in order to add event handlers to it
                HandleSource.AddHook(connector.HandleSimConnectEvents);

                connector.Initialize(Handle);

                viewModel.SimConnectState = SimConnectState.Connected;
            }
            catch
            {
                MessageBox.Show("Cannot initialize SimConnect");
            }
            stopwatch.Start();
        }

        private void Connector_AircraftPositionUpdated(object sender, AircraftPositionUpdatedEventArgs e)
        {
            if (startMilliseconds.HasValue && !endMilliseconds.HasValue && records != null)
            {
                records.Add((stopwatch.ElapsedMilliseconds, e.Position));
                var count = records.Count;
                Dispatcher.Invoke(() =>
                {
                    viewModel.FrameCount = count;
                });
            }

            Dispatcher.Invoke(() =>
            {
                viewModel.AircraftPosition = AircraftPosition.FromStruct(e.Position);
            });
        }

        private void ButtonRecord_Click(object sender, RoutedEventArgs e)
        {
            logger.LogDebug("Start recording...");

            viewModel.State = State.Recording;

            startMilliseconds = stopwatch.ElapsedMilliseconds;
            endMilliseconds = null;
            records = new List<(long milliseconds, AircraftPositionStruct position)>();
        }

        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            endMilliseconds = stopwatch.ElapsedMilliseconds;

            viewModel.FrameCount = records.Count;
            viewModel.State = State.Idle;
            logger.LogDebug("Recording stopped. {totalFrames} frames recorded.", records.Count);
        }

        private void ButtonReplay_Click(object sender, RoutedEventArgs e)
        {
            logger.LogDebug("Start replay...");

            viewModel.State = State.Replaying;

            replayMilliseconds = stopwatch.ElapsedMilliseconds;

            Task.Run(() =>
            {
                connector.Pause();

                var enumerator = records.GetEnumerator();
                long? recordedElapsed = null;
                AircraftPositionStruct? position = null;

                long? lastElapsed = 0;
                AircraftPositionStruct? lastPosition = null;

                var frameIndex = 0;

                while (true)
                {
                    var currentElapsed = stopwatch.ElapsedMilliseconds - replayMilliseconds;

                    while (!recordedElapsed.HasValue || currentElapsed > recordedElapsed)
                    {
                        logger.LogTrace("Move next.", currentElapsed);
                        var canMove = enumerator.MoveNext();

                        if (canMove)
                        {
                            frameIndex++;
                            (var recordedMilliseconds, var recordedPosition) = enumerator.Current;
                            lastElapsed = recordedElapsed;
                            lastPosition = position;
                            recordedElapsed = recordedMilliseconds - startMilliseconds;
                            position = recordedPosition;

                            Dispatcher.Invoke(() =>
                            {
                                viewModel.CurrentFrame = frameIndex;
                            });
                        }
                        else
                        {
                            logger.LogDebug("Replay finished.");
                            connector.Unpause();
                            Dispatcher.Invoke(() =>
                            {
                                viewModel.State = State.Idle;
                            });
                            return;
                        }
                    }

                    if (position.HasValue)
                    {
                        logger.LogTrace("Delta time {delta} {current} {recorded}.", currentElapsed - recordedElapsed, currentElapsed, recordedElapsed);

                        var nextValue = position.Value;
                        if (lastPosition.HasValue)
                        {
                            var interpolation = (double)(currentElapsed - lastElapsed.Value) / (recordedElapsed.Value - lastElapsed.Value);
                            nextValue = nextValue * interpolation + lastPosition.Value * (1 - interpolation);
                        }

                        connector.Set(nextValue);
                    }

                    Thread.Sleep(16);
                }
            });
        }
    }
}
