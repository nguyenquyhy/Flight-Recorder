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

        private ServiceProvider serviceProvider;
        private MainWindow mainWindow;

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

            serviceProvider = serviceCollection.BuildServiceProvider();

            mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                var recorderLogic = serviceProvider?.GetService<IRecorderLogic>();
                if (recorderLogic != null)
                {
                    recorderLogic.Unfreeze();
                }
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

            services.AddSingleton<Connector>();
            services.AddSingleton<IRecorderLogic, RecorderLogic>();
            services.AddSingleton<ImageLogic>();
            services.AddSingleton<ExportLogic>();
            services.AddTransient<ThrottleLogic>();
            services.AddTransient(typeof(MainViewModel));
            services.AddTransient(typeof(MainWindow));
        }
    }
}
