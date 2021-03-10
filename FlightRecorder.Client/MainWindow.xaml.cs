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
        private readonly IRecorderLogic recorderLogic;
        private readonly ImageLogic imageLogic;
        private readonly ExportLogic exportLogic;
        private readonly ThrottleLogic drawingThrottleLogic;
        private readonly StateMachine stateMachine;
        private readonly string currentVersion;

        private IntPtr Handle;

        public MainWindow(ILogger<MainWindow> logger, MainViewModel viewModel, Connector connector, IRecorderLogic recorderLogic, ImageLogic imageLogic, ExportLogic exportLogic, ThrottleLogic drawingThrottleLogic, StateMachine stateMachine)
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
            this.stateMachine = stateMachine;

            stateMachine.StateChanged += StateMachine_StateChanged;

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

        private void StateMachine_StateChanged(object sender, StateChangedEventArgs e)
        {
            if (e.By == StateMachine.Event.Stop)
            {
                Draw(false);
            }
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

            Dispatcher.Invoke(async () =>
            {
                await stateMachine.TransitAsync(StateMachine.Event.Stop);
            });
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await stateMachine.TransitAsync(StateMachine.Event.StartUp);

            viewModel.SimConnectState = SimConnectState.Connecting;

            // Create an event handle for the WPF window to listen for SimConnect events
            Handle = new WindowInteropHelper(sender as Window).Handle; // Get handle of main WPF Window
            var HandleSource = HwndSource.FromHwnd(Handle); // Get source of handle in order to add event handlers to it
            HandleSource.AddHook(HandleHook);
            InitializeConnector();
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (stateMachine.CurrentState != StateMachine.State.End)
            {
                e.Cancel = true;
                if (await stateMachine.TransitAsync(StateMachine.Event.Exit))
                {
                    Application.Current.Shutdown();
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

        private void Connector_AircraftPositionUpdated(object sender, AircraftPositionUpdatedEventArgs e)
        {
            recorderLogic.NotifyPosition(e.Position);

            Dispatcher.Invoke(() =>
            {
                viewModel.AircraftPosition = AircraftPosition.FromStruct(e.Position);
            });
        }

        private void Connector_Frame(object sender, EventArgs e)
        {
            recorderLogic.Tick();
        }

        private async void Connector_Closed(object sender, EventArgs e)
        {
            await stateMachine.TransitAsync(StateMachine.Event.Disconnect);

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

        private void ButtonChangeSpeed_Click(object sender, RoutedEventArgs e)
        {
            (sender as Button).ContextMenu.IsOpen = true;
        }

        private async void ButtonReplay_Click(object sender, RoutedEventArgs e)
        {
            await stateMachine.TransitAsync(StateMachine.Event.Replay);
        }

        private async void ButtonPauseReplay_Click(object sender, RoutedEventArgs e)
        {
            await stateMachine.TransitAsync(StateMachine.Event.Pause);
        }

        private async void ButtonResumeReplay_Click(object sender, RoutedEventArgs e)
        {
            await stateMachine.TransitAsync(StateMachine.Event.Resume);
        }

        private async void ButtonStopReplay_Click(object sender, RoutedEventArgs e)
        {
            await stateMachine.TransitAsync(StateMachine.Event.RequestStopping);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            recorderLogic.Seek((int)e.NewValue);
            Draw(viewModel.IsThrottlingChart);
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

        private async void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (!recorderLogic.CanSave)
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

                    await stateMachine.TransitAsync(StateMachine.Event.Save);
                }

                logger.LogDebug("Save file into {fileName}", dialog.FileName);
            }
        }

        private async void ButtonExport_Click(object sender, RoutedEventArgs e)
        {
            if (!recorderLogic.CanSave)
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

        private async void ButtonLoad_Click(object sender, RoutedEventArgs e)
        {
            if (await stateMachine.TransitAsync(StateMachine.Event.Load))
            {
                imageLogic.ClearCache();
                Draw();
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
                        await stateMachine.TransitAsync(StateMachine.Event.Connect);
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
