using FlightRecorder.Client.Logics;
using FlightRecorder.Client.SimConnectMSFS;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
        private readonly ImageLogic imageLogic;
        private readonly ExportLogic exportLogic;
        private readonly ThrottleLogic drawingThrottleLogic;
        private readonly string currentVersion;

        private IntPtr Handle;

        public MainWindow(ILogger<MainWindow> logger, MainViewModel viewModel, Connector connector, RecorderLogic recorderLogic, ImageLogic imageLogic, ExportLogic exportLogic, ThrottleLogic drawingThrottleLogic)
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.logger = logger;
            this.viewModel = viewModel;
            this.connector = connector;
            this.recorderLogic = recorderLogic;
            this.imageLogic = imageLogic;
            this.exportLogic = exportLogic;
            this.drawingThrottleLogic = drawingThrottleLogic;
            connector.AircraftPositionUpdated += Connector_AircraftPositionUpdated;
            connector.Frame += Connector_Frame;
            connector.Closed += Connector_Closed;

            DataContext = viewModel;

            recorderLogic.RecordsUpdated += RecorderLogic_RecordsUpdated;
            recorderLogic.CurrentFrameChanged += RecorderLogic_CurrentFrameChanged;
            recorderLogic.ReplayFinished += RecorderLogic_ReplayFinished;

            currentVersion = Assembly.GetEntryAssembly().GetName().Version.ToString();
            Title += " " + currentVersion;
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
            try
            {
                Dispatcher.Invoke(() =>
                {
                    viewModel.CurrentFrame = e.CurrentFrame;
                });
            }
            catch (TaskCanceledException)
            {
                // Exiting
            }
        }

        private void RecorderLogic_ReplayFinished(object sender, EventArgs e)
        {
            imageLogic.ClearCache();

            Dispatcher.Invoke(() =>
            {
                viewModel.State = State.Idle;
                viewModel.CurrentFrame = 0;

                Draw();
            });
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            viewModel.SimConnectState = SimConnectState.Connecting;

            // Create an event handle for the WPF window to listen for SimConnect events
            Handle = new WindowInteropHelper(sender as Window).Handle; // Get handle of main WPF Window
            var HandleSource = HwndSource.FromHwnd(Handle); // Get source of handle in order to add event handlers to it
            HandleSource.AddHook(HandleHook);
            InitializeConnector();
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

        private void Connector_AircraftPositionUpdated(object sender, AircraftPositionUpdatedEventArgs e)
        {
            recorderLogic.CurrentPosition = e.Position;

            Dispatcher.Invoke(() =>
            {
                viewModel.AircraftPosition = AircraftPosition.FromStruct(e.Position);
            });
        }

        private void Connector_Frame(object sender, EventArgs e)
        {
            recorderLogic.Tick();
        }

        private void Connector_Closed(object sender, EventArgs e)
        {
            logger.LogDebug("Start reconnecting...");
            InitializeConnector();
        }

        private void ButtonRecord_Click(object sender, RoutedEventArgs e)
        {
            recorderLogic.Start();
            viewModel.State = State.Recording;
            viewModel.CurrentFrame = 0;
        }

        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            recorderLogic.StopRecording();
            viewModel.State = State.Idle;
            Draw(false);
            SaveRecording();
        }

        private void ButtonChangeSpeed_Click(object sender, RoutedEventArgs e)
        {
            (sender as Button).ContextMenu.IsOpen = true;
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
            recorderLogic.Seek((int)e.NewValue);
            Draw(viewModel.State == State.Recording || viewModel.State == State.Replaying);
        }

        private void Slider_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
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

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            SaveRecording();
        }

        private void SaveRecording()
        {
            if (!recorderLogic.IsEnded || !recorderLogic.IsReplayable)
            {
                MessageBox.Show("Nothing to save!");
                return;
            }

            var dialog = new SaveFileDialog
            {
                FileName = $"{DateTime.Now:yyyy-MM-dd-HH-mm}.flightrecorder",
                Filter = "Recorded Flight|*.flightrecorder"
            };
            if (dialog.ShowDialog() == true)
            {
                var data = recorderLogic.ToData(currentVersion);
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

        private async void ButtonExport_Click(object sender, RoutedEventArgs e)
        {
            if (!recorderLogic.IsEnded || !recorderLogic.IsReplayable)
            {
                MessageBox.Show("Nothing to export!");
                return;
            }

            var dialog = new SaveFileDialog
            {
                FileName = $"Export {DateTime.Now:yyyy-MM-dd-HH-mm}.csv",
                Filter = "CSV (for Excel)|*.csv"
            };
            if (dialog.ShowDialog() == true)
            {
                await exportLogic.ExportAsync(dialog.FileName, recorderLogic.Records.Select(o =>
                {
                    var result = AircraftPosition.FromStruct(o.position);
                    result.Milliseconds = o.milliseconds;
                    return result;
                }));

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
                try
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
                            imageLogic.ClearCache();

                            Draw();
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Cannot load file");
                    MessageBox.Show("The selected file is not a valid recording or not accessible!\n\nAre you sure you are opening a *.flightrecorder file?", "Flight Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ButtonShowData_Click(object sender, RoutedEventArgs e)
        {
            viewModel.ShowData = !viewModel.ShowData;
            Height = viewModel.ShowData ? 420 : 275;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Draw(false);
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as MenuItem).Header is string header && double.TryParse(header[1..], NumberStyles.Any, CultureInfo.InvariantCulture, out var rate))
            {
                ButtonSpeed.Content = header;
                recorderLogic.ChangeRate(rate);
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
                        viewModel.SimConnectState = SimConnectState.Connected;
                        recorderLogic.Initialize();
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

        private void Draw(bool throttle = true)
        {
            var width = (int)ImageWrapper.ActualWidth;
            var height = (int)ImageWrapper.ActualHeight;
            var currentFrame = viewModel.CurrentFrame;

            Task.Run(() =>
            {
                if (throttle)
                {
                    drawingThrottleLogic.RunAsync(async () => Draw(width, height, currentFrame), 500);
                }
                else
                {
                    Draw(width, height, currentFrame);
                }
            });
        }

        private void Draw(int width, int height, int currentFrame)
        {
            try
            {
                var image = imageLogic.Draw(width, height, recorderLogic.Records, currentFrame);

                if (image != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var bmp = new WriteableBitmap(image.Width, image.Height, image.Metadata.HorizontalResolution, image.Metadata.VerticalResolution, PixelFormats.Bgra32, null);

                            bmp.Lock();
                            try
                            {
                                var backBuffer = bmp.BackBuffer;

                                for (var y = 0; y < image.Height; y++)
                                {
                                    var buffer = image.GetPixelRowSpan(y);
                                    for (var x = 0; x < image.Width; x++)
                                    {
                                        var backBufferPos = backBuffer + (y * image.Width + x) * 4;
                                        var rgba = buffer[x];
                                        var color = rgba.A << 24 | rgba.R << 16 | rgba.G << 8 | rgba.B;

                                        Marshal.WriteInt32(backBufferPos, color);
                                    }
                                }

                                bmp.AddDirtyRect(new Int32Rect(0, 0, image.Width, image.Height));
                            }
                            finally
                            {
                                bmp.Unlock();
                                ImageChart.Source = bmp;
                            }
                        }
                        catch (Exception ex)
                        {
#if DEBUG
                            logger.LogError(ex, "Cannot convert to WriteableBitmap");
#endif
                        }
                        finally
                        {
                            image.Dispose();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                logger.LogError(ex, "Cannot draw");
#endif
            }
        }
    }
}
