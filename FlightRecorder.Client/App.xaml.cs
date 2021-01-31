using FlightRecorder.Client.SimConnectMSFS;
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
        private MainWindow mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
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
            services.AddTransient(typeof(MainViewModel));
            services.AddTransient(typeof(MainWindow));
        }
    }
}
