using FlightRecorder.Client.SimConnectMSFS;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
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
        private AircraftPositionStruct? currentPosition = null;

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
            viewModel.SimConnectState = SimConnectState.Connecting;

            // Create an event handle for the WPF window to listen for SimConnect events
            Handle = new WindowInteropHelper(sender as Window).Handle; // Get handle of main WPF Window
            var HandleSource = HwndSource.FromHwnd(Handle); // Get source of handle in order to add event handlers to it
            HandleSource.AddHook(HandleHook);

            try
            {
                connector.Initialize(Handle);

                Dispatcher.Invoke(() =>
                {
                    viewModel.SimConnectState = SimConnectState.Connected;
                });
            }
            catch (BadImageFormatException ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Cannot initialize SimConnect");
                    viewModel.SimConnectState = SimConnectState.Failed;
                });
            }
            catch
            {
                viewModel.SimConnectState = SimConnectState.NotConnected;

                // TODO: retry
            }
            stopwatch.Start();
        }

        private IntPtr HandleHook(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam, ref bool isHandled)
        {
            try
            {
                connector.HandleSimConnectEvents(message, ref isHandled);
                return IntPtr.Zero;
            }
            catch (BadImageFormatException ex)
            {
                return IntPtr.Zero;
            }
        }

        private void Connector_AircraftPositionUpdated(object sender, AircraftPositionUpdatedEventArgs e)
        {
            currentPosition = e.Position;

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
            if (records == null || records.Count == 0)
            {
                MessageBox.Show("Nothing to replay");
                return;
            }

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
                    var replayStartTime = replayMilliseconds;
                    if (replayStartTime == null)
                    {
                        FinishReplay();
                        return;
                    }

                    var currentElapsed = stopwatch.ElapsedMilliseconds - replayStartTime.Value;

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
                            FinishReplay();
                            return;
                        }
                    }

                    if (position.HasValue)
                    {
                        logger.LogTrace("Delta time {delta} {current} {recorded}.", currentElapsed - recordedElapsed, currentElapsed, recordedElapsed);

                        var nextValue = AircraftPositionStructOperator.ToSet(position.Value);
                        if (lastPosition.HasValue)
                        {
                            var interpolation = (double)(currentElapsed - lastElapsed.Value) / (recordedElapsed.Value - lastElapsed.Value);
                            if (interpolation == 0.5)
                            {
                                // Edge case: let next value win
                                interpolation = 0.501;
                            }
                            nextValue = nextValue * interpolation + AircraftPositionStructOperator.ToSet(lastPosition.Value) * (1 - interpolation);
                        }
                        if (currentPosition.HasValue)
                        {
                            connector.TriggerEvents(currentPosition.Value, position.Value);
                        }

                        connector.Set(nextValue);
                    }

                    Thread.Sleep(16);
                }
            });
        }

        private void ButtonStopReplay_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.State == State.Replaying)
            {
                replayMilliseconds = null;
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (records == null || records.Count == 0 || !startMilliseconds.HasValue || !endMilliseconds.HasValue)
            {
                MessageBox.Show("Nothing to save!");
                return;
            }

            var dialog = new SaveFileDialog
            {
                FileName = $"{DateTime.Now:yyyy-MM-dd-HH-mm}.flightrecorder"
            };
            if (dialog.ShowDialog() == true)
            {
                var version = Assembly.GetEntryAssembly().GetName().Version;
                var data = new SavedData(version.ToString(), startMilliseconds.Value, endMilliseconds.Value, records);
                var dataString = JsonSerializer.Serialize(data);

                using (var fileStream = new FileStream(dialog.FileName, FileMode.Create))
                {
                    using var outStream = new ZipOutputStream(fileStream);

                    outStream.SetLevel(9);

                    var entry = new ZipEntry("data.json")
                    {
                        DateTime = DateTime.Now
                    };
                    outStream.PutNextEntry(entry);

                    var writer = new StreamWriter(outStream);
                    writer.Write(dataString);
                    writer.Flush();

                    outStream.Finish();
                }

                logger.LogDebug("Save file into {fileName}", dialog.FileName);
            }
        }

        private void ButtonLoad_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Recorded Flight|*.flightrecorder"
            };

            if (dialog.ShowDialog() == true)
            {
                using var file = dialog.OpenFile();
                using var zipFile = new ZipFile(file);

                foreach (ZipEntry entry in zipFile)
                {
                    if (entry.IsFile && entry.Name == "data.json")
                    {
                        using var stream = zipFile.GetInputStream(entry);

                        var reader = new StreamReader(stream);
                        var dataString = reader.ReadToEnd();

                        var savedData = JsonSerializer.Deserialize<SavedData>(dataString);

                        startMilliseconds = savedData.StartTime;
                        endMilliseconds = savedData.EndTime;
                        records = savedData.Records.Select(r => (r.Time, AircraftPosition.ToStruct(r.Position))).ToList();
                        viewModel.FrameCount = records.Count;
                    }
                }
            }
        }

        private void FinishReplay()
        {
            logger.LogDebug("Replay finished.");
            connector.Unpause();
            Dispatcher.Invoke(() =>
            {
                viewModel.State = State.Idle;
                viewModel.CurrentFrame = 0;
            });
        }
    }

    public class SavedData
    {
        public SavedData()
        {

        }

        public SavedData(string clientVersion, long startTime, long endTime, List<(long milliseconds, AircraftPositionStruct position)> records)
        {
            ClientVersion = clientVersion;
            StartTime = startTime;
            EndTime = endTime;
            Records = records.Select(r => new SavedRecord
            {
                Time = r.milliseconds,
                Position = AircraftPosition.FromStruct(r.position)
            }).ToList();
        }

        public string ClientVersion { get; set; }
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public List<SavedRecord> Records { get; set; }

        public class SavedRecord
        {
            public long Time { get; set; }
            public AircraftPosition Position { get; set; }
        }
    }
}
