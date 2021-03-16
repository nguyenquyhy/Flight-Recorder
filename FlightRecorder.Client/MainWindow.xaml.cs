using FlightRecorder.Client.Logics;
using FlightRecorder.Client.SimConnectMSFS;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Diagnostics;
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
        private readonly UserConfig userConfig;
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);



        #region HotKeys Constants
        private const int HOTKEY_ID = 9000;
        //Modifiers:
        private const uint MOD_CONTROL = 0x0002; //CTRL
        private const uint MOD_SHIFT = 0x0004; //SHIFT
        //VK's
        private const uint VK_R = 0x52; // R
        private const uint VK_P = 0x50; // P
        private const uint VK_C = 0x43; // C
        private const uint VK_S = 0x53; // S
        private const uint VK_PRIOR = 0x0021; //PAGE UP key
        private const uint VK_NEXT = 0x0022; //PAGE DOWN key
        private const uint VK_LEFT = 0x25; //LEFT ARROW key
        private const uint VK_UP = 0x26; //UP ARROW key
        private const uint VK_RIGHT = 0x27; //RIGHT ARROW key
        private const uint VK_DOWN = 0x28; // DOWN ARROW key
        private const uint VK_END = 0x23; // END key
        #endregion HotKeys Constants

        private HwndSource _source;
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

    
            userConfig = new();
            this.chkAutoUpdate.IsChecked = userConfig.AutomaticUpdate;

           

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
            Height = viewModel.ShowData ? 450 : 275;
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

        #region HotKeys
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            Handle = new WindowInteropHelper(this).Handle;
            _source = HwndSource.FromHwnd(Handle);
            _source.AddHook(HwndHook);

            #region Register HOT Keys
            RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_R);  //Record
            RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_S);  //StopRecord
            RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_UP);   //Start Replay 
            RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_RIGHT); //Avançar 
            RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_LEFT); //Recuar
            RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_DOWN); //Pause Replay 
            RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_DOWN); //Resume Replay
            RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_END);   //Stop Replay 
            RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_PRIOR); //Aumenta Velocidade
            RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_NEXT); //Diminue Velocidade
            #endregion Register HOT Keys

            #region Updater
            if((bool)this.chkAutoUpdate.IsChecked)
            {
                string dir_app = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName).ToString();
                if (Directory.Exists(dir_app + @"\Updater"))
                {
                    if (File.Exists(dir_app + @"\Updater\FlightRecorder.Updater.exe"))
                    {
                        var currentVersion = Assembly.GetEntryAssembly().GetName().Version.ToString();
                        var client_github = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("Flight-Recorder"));
                        var releases = client_github.Repository.Release.GetAll("nguyenquyhy", "Flight-Recorder").Result;
                        var latest = releases[0];
                        //currentVersion = "0.10";
                        string version = latest.Name.Replace("Version", "").Trim();
                        if (currentVersion.IndexOf(version) < 0)
                        {
                            if (MessageBox.Show("A new version of Flight Record is available. Do you want to upgrade now ?", "Attention", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                            {
                                Process.Start(dir_app + @"\Updater\FlightRecorder.Updater.exe");
                                Application.Current.Shutdown();
                            }
                        }
                    }
                }
            }

            #endregion Updater
        }


        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            try
            {
                const int WM_HOTKEY = 0x0312;
                switch (msg)
                {
                    case WM_HOTKEY:
                        switch (wParam.ToInt32())
                        {
                            case HOTKEY_ID:
                                int vkey = (((int)lParam >> 16) & 0xFFFF);
                                if (vkey == VK_R)
                                {
                                    if (viewModel.State != StateMachine.State.Recording)
                                    {
                                        startRecord();
                                    }

                                }
                                else if (vkey == VK_S)
                                {
                                    if (viewModel.State == StateMachine.State.Recording)
                                    {
                                        stopRecord();
                                    }
                                }
                                else if (vkey == VK_UP)
                                {
                                    if (viewModel.State != StateMachine.State.Recording)
                                    {
                                        if (viewModel.State == StateMachine.State.PausingSaved || viewModel.State == StateMachine.State.PausingUnsaved)
                                        {
                                            resumeReplay();
                                        }
                                        else
                                        {
                                            startReplay();
                                        }
                                    }
                                }
                                else if (vkey == VK_DOWN)
                                {
                                    if (viewModel.State != StateMachine.State.Recording)
                                    {
                                        if (viewModel.State == StateMachine.State.ReplayingSaved || viewModel.State == StateMachine.State.ReplayingUnsaved)
                                            pauseReplay();
                                    }
                                }
                                else if (vkey == VK_END)
                                {
                                    if (viewModel.State != StateMachine.State.Recording)
                                    {
                                        if (viewModel.State == StateMachine.State.ReplayingSaved || viewModel.State == StateMachine.State.ReplayingUnsaved)
                                            stopReplay();
                                    }

                                }
                                else if (vkey == VK_RIGHT)
                                {
                                    if (viewModel.State != StateMachine.State.Recording)
                                    //if (viewModel.State != State.Recording)
                                    {
                                        if (viewModel.State == StateMachine.State.ReplayingSaved || viewModel.State == StateMachine.State.ReplayingUnsaved)
                                            pauseReplay();
                                        var currentFrame = viewModel.CurrentFrame;

                                        if (currentFrame < viewModel.FrameCount - 5)
                                        {
                                            viewModel.CurrentFrame = currentFrame + 5;
                                        }
                                        else
                                        {
                                            viewModel.CurrentFrame = viewModel.FrameCount;
                                        }

                                        resumeReplay();

                                    }

                                }
                                else if (vkey == VK_LEFT)
                                {
                                    if (viewModel.State != StateMachine.State.Recording)
                                    {
                                        if (viewModel.State == StateMachine.State.ReplayingSaved || viewModel.State == StateMachine.State.ReplayingUnsaved)
                                            pauseReplay();

                                        var currentFrame = viewModel.CurrentFrame;
                                        if (currentFrame >= 5)
                                        {
                                            viewModel.CurrentFrame = currentFrame - 5;
                                        }
                                        else
                                        {
                                            viewModel.CurrentFrame = 0;
                                        }

                                        resumeReplay();

                                    }


                                }
                                else if (vkey == VK_PRIOR)
                                {
                                    if (viewModel.State != StateMachine.State.Recording)
                                    {
                                        if (viewModel.State == StateMachine.State.ReplayingSaved || viewModel.State == StateMachine.State.ReplayingUnsaved)
                                            pauseReplay();
                                        double rate = recorderLogic.GetRate();
                                        if (rate >= 1 && rate < 16)
                                        {
                                            rate += 0.5;
                                            recorderLogic.ChangeRate(rate);
                                            ButtonSpeed.Content = "x" + rate.ToString(CultureInfo.InvariantCulture);


                                        }
                                        else
                                        {
                                            rate += 0.25;
                                            recorderLogic.ChangeRate(rate);
                                            ButtonSpeed.Content = "x" + rate.ToString(CultureInfo.InvariantCulture);
                                        }
                                        resumeReplay();

                                    }

                                }
                                else if (vkey == VK_NEXT)
                                {
                                    if (viewModel.State != StateMachine.State.Recording)
                                    {
                                        if (viewModel.State == StateMachine.State.ReplayingSaved || viewModel.State == StateMachine.State.ReplayingUnsaved)
                                            pauseReplay();
                                        double rate = recorderLogic.GetRate();
                                        if (rate >= 1 && rate < 16)
                                        {
                                            rate -= 0.5;
                                            recorderLogic.ChangeRate(rate);
                                            ButtonSpeed.Content = "x" + rate.ToString(CultureInfo.InvariantCulture);


                                        }
                                        else if (rate >= 0.25 && rate < 1)
                                        {
                                            rate -= 0.25;
                                            recorderLogic.ChangeRate(rate);
                                            ButtonSpeed.Content = "x" + rate.ToString(CultureInfo.InvariantCulture);
                                        }
                                        resumeReplay();
                                    }

                                }
                                handled = true;
                                break;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                logger.LogError(ex, "HotKey Error");
#endif
            }

            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            if(_source == null)
            {
                _source.RemoveHook(HwndHook);
                UnregisterHotKey(Handle, HOTKEY_ID);
            }
            base.OnClosed(e);
        }
        #endregion HotKeys

        #region Actions
        private async void startRecord()
        {
            await stateMachine.TransitAsync(StateMachine.Event.Record);
        }


        private async void stopRecord()
        {
            await stateMachine.TransitAsync(StateMachine.Event.Stop);
        }

        private async void startReplay()
        {
            await stateMachine.TransitAsync(StateMachine.Event.Replay);
        }

        private async void pauseReplay()
        {
            await stateMachine.TransitAsync(StateMachine.Event.Pause);
        }


        private async void resumeReplay()
        {
            await stateMachine.TransitAsync(StateMachine.Event.Resume);
        }

        private async void stopReplay()
        {
            await stateMachine.TransitAsync(StateMachine.Event.RequestStopping);
        }
        #endregion Actions

        private void chkAutoUpdate_CheckedChanged(object sender, RoutedEventArgs e)
        {
            userConfig.SetAutomaticUpdateValue((bool)chkAutoUpdate.IsChecked);
        }

    }
}
