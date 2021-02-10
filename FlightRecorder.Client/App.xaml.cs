using FlightRecorder.Client.Logics;
using FlightRecorder.Client.SimConnectMSFS;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Windows;

namespace FlightRecorder.Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider serviceProvider;
        private MainWindow mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            AppCenter.Start("5525090f-eddc-4bca-bdd9-5b5fdc301ed0", typeof(Analytics), typeof(Crashes));

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            serviceProvider = serviceCollection.BuildServiceProvider();

            mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (Log.Logger != null)
            {
                Log.CloseAndFlush();
            }
            try
            {
                serviceProvider?.GetService<Connector>()?.Unpause();
            }
            catch { }
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
            services.AddSingleton<RecorderLogic>();
            services.AddSingleton<ImageLogic>();
            services.AddTransient(typeof(MainViewModel));
            services.AddTransient(typeof(MainWindow));
        }
    }
}
