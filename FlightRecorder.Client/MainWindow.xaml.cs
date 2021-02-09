using FlightRecorder.Client.Logics;
using FlightRecorder.Client.SimConnectMSFS;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
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
        private readonly RecorderLogic recorderLogic;

        private IntPtr Handle;

        public MainWindow(ILogger<MainWindow> logger, MainViewModel viewModel, Connector connector, RecorderLogic recorderLogic)
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.logger = logger;
            this.viewModel = viewModel;
            this.connector = connector;
            this.recorderLogic = recorderLogic;
            connector.AircraftPositionUpdated += Connector_AircraftPositionUpdated;

            DataContext = viewModel;

            recorderLogic.RecordsUpdated += RecorderLogic_RecordsUpdated;
            recorderLogic.CurrentFrameChanged += RecorderLogic_CurrentFrameChanged;
            recorderLogic.ReplayFinished += RecorderLogic_ReplayFinished;
        }

        private void RecorderLogic_RecordsUpdated(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                viewModel.FrameCount = recorderLogic.Records?.Count ?? 0;
            });
        }

        private void RecorderLogic_CurrentFrameChanged(object sender, CurrentFrameChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                viewModel.CurrentFrame = e.CurrentFrame;
            });
        }

        private void RecorderLogic_ReplayFinished(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                viewModel.State = State.Idle;
                viewModel.CurrentFrame = 0;
            });
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

            recorderLogic.Initialize();
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
            recorderLogic.CurrentPosition = e.Position;

            Dispatcher.Invoke(() =>
            {
                viewModel.AircraftPosition = AircraftPosition.FromStruct(e.Position);
            });
        }

        private void ButtonRecord_Click(object sender, RoutedEventArgs e)
        {
            recorderLogic.Start();
            viewModel.State = State.Recording;
        }

        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            recorderLogic.StopRecording();
            viewModel.State = State.Idle;
        }

        private void ButtonReplay_Click(object sender, RoutedEventArgs e)
        {
            if (!recorderLogic.Replay())
            {
                MessageBox.Show("Nothing to replay");
                return;
            }

            viewModel.State = State.Replaying;


        }

        private void ButtonPauseReplay_Click(object sender, RoutedEventArgs e)
        {
            if (recorderLogic.PauseReplay())
            {
                viewModel.State = State.Pausing;
            }
        }

        private void ButtonResumeReplay_Click(object sender, RoutedEventArgs e)
        {
            if (recorderLogic.ResumeReplay())
            {
                viewModel.State = State.Replaying;
            }
        }

        private void ButtonStopReplay_Click(object sender, RoutedEventArgs e)
        {
            if (recorderLogic.StopReplay())
            {
                // NOTE: state transit does not happen here, it will hapen when the loop is broken
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (viewModel.State == State.Pausing)
            {
                recorderLogic.CurrentFrame = (int)e.NewValue;
            }
        }

        private void Slider_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            switch (e.Delta)
            {
                case > 0:
                    recorderLogic.CurrentFrame += 1;
                    break;
                case < 0:
                    recorderLogic.CurrentFrame -= 1;
                    break;
            }
            viewModel.CurrentFrame = recorderLogic.CurrentFrame;
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (!recorderLogic.IsEnded || !recorderLogic.IsReplayable)
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
                var data = recorderLogic.ToData(version.ToString());
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

                        recorderLogic.FromData(savedData);
                    }
                }
            }
        }
    }
}
