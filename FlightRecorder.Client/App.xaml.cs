using FlightRecorder.Client.Logics;
using FlightRecorder.Client.SimConnectMSFS;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Sink.AppCenter;
using System;
using System.Linq;
using System.Windows;

namespace FlightRecorder.Client;

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

    public ServiceProvider? ServiceProvider { get; private set; }

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

        MainWindow = ServiceProvider.GetRequiredService<WindowFactory>().Create<MainWindow>(ServiceProvider);
        MainWindow.Show();
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
                .WriteTo.File("logs/flightrecorder.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 3, buffered: true)
            )
            .WriteTo.Logger(config => config
                .MinimumLevel.Information()
                .WriteTo.AppCenterSink(target: AppCenterTarget.ExceptionsAsEvents)
            )
            .CreateLogger();

        services.AddLogging(configure =>
        {
            configure.AddSerilog();
        });

        services.AddSingleton<ICrashLogic, CrashLogic>();
        services.AddSingleton<VersionLogic>();
        services.AddSingleton<IStorageLogic, StorageLogic>();
        services.AddSingleton<IThreadLogic, ThreadLogic>();
        services.AddSingleton<IConnector, Connector>();
        services.AddSingleton<CsvExportLogic>();
        services.AddSingleton<KmlExportLogic>();
        services.AddSingleton<IDialogLogic, DialogLogic>();
        services.AddSingleton<WindowFactory>();
        services.AddSingleton<ISettingsLogic, FileSettingsLogic>();

        services.AddScoped<Orchestrator>();
        services.AddScoped<StateMachine>();
        services.AddScoped<ShortcutKeyLogic>();

        // NOTE: For recorder, we leave it as singleton as it is supported only on main windows (not AI) and to allow saving on crash.
        services.AddSingleton<IRecorderLogic, RecorderLogic>();
        // NOTE: For replay, we set it as scoped as we need to create a new instance for each window.
        services.AddScoped<IReplayLogic, ReplayLogic>();

        services.AddScoped<MainViewModel>();
        services.AddScoped<MainWindow>();
        services.AddScoped<AIWindow>();
        services.AddScoped<ShortcutKeysWindow>();

        services.AddTransient<DrawingLogic>();
        services.AddTransient<ImageLogic>();
        services.AddTransient<ThrottleLogic>();
    }

    private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var crashLogic = ServiceProvider?.GetRequiredService<ICrashLogic>();
            crashLogic?.SaveData();
        }
        catch
        {
            // Ignore
        }
    }
}
