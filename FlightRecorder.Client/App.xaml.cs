using FlightRecorder.Client.Logics;
using FlightRecorder.Client.SimConnectMSFS;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Linq;
using System.Windows;

namespace FlightRecorder.Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        #region Single Instance Enforcer

        readonly SingletonApplicationEnforcer enforcer = new SingletonApplicationEnforcer(args =>
        {
            Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = Current.MainWindow as MainWindow;
                if (mainWindow != null && args != null)
                {
                    mainWindow.RestoreWindow();
                }
            });
        }, "FlightRecorder.Client");

        #endregion

        public ServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            if (!e.Args.Contains("--multiple-instances") && enforcer.ShouldApplicationExit())
            {
                try
                {
                    Shutdown();
                }
                catch { }
            }

#if DEBUG
            AppCenter.Start("cd6afedd-0333-4624-af0c-91eaf5273f15", typeof(Analytics), typeof(Crashes));
#else
            AppCenter.Start("5525090f-eddc-4bca-bdd9-5b5fdc301ed0", typeof(Analytics), typeof(Crashes));
#endif

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            using (var scope = ServiceProvider.CreateScope())
            {
                MainWindow = scope.ServiceProvider.GetRequiredService<MainWindow>();
                MainWindow.Show();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // This create a new instance of ReplayLogic, which is not desirable.
                // However, it can still unfreeze the aircraft.
                var replayLogic = ServiceProvider?.GetService<IReplayLogic>();
                replayLogic?.Unfreeze();
            }
            catch
            {
                // Ignore
            }

            if (Log.Logger != null)
            {
                Log.CloseAndFlush();
            }

            base.OnExit(e);
        }

        private void ConfigureServices(ServiceCollection services)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Debug()
                .WriteTo.Logger(config => config
                    .MinimumLevel.Information()
                    .WriteTo.File("flightrecorder.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 3, buffered: true)
                )
                .CreateLogger();

            services.AddLogging(configure =>
            {
                configure.AddSerilog();
            });

            services.AddSingleton<IThreadLogic, ThreadLogic>();
            services.AddSingleton<IConnector, Connector>();
            services.AddSingleton<ExportLogic>();
            services.AddSingleton<IDialogLogic, DialogLogic>();

            services.AddScoped<Orchestrator>();
            services.AddScoped<StateMachine>();
            services.AddScoped<IRecorderLogic, RecorderLogic>();
            services.AddScoped<IReplayLogic, ReplayLogic>();
            services.AddScoped<MainViewModel>();
            services.AddScoped<MainWindow>();
            services.AddScoped<AIWindow>();

            services.AddTransient<DrawingLogic>();
            services.AddTransient<ImageLogic>();
            services.AddTransient<ThrottleLogic>();
        }
    }
}
