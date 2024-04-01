using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;

namespace FlightRecorder.Client.Logics;

public class WindowFactory
{
    public W CreateScopedWindow<W>(IServiceProvider serviceProvider) where W : Window
    {
        var scope = serviceProvider.CreateScope();
        var window = scope.ServiceProvider.GetRequiredService<W>();
        window.Closed += (sender, agrs) =>
        {
            scope.Dispose();
        };
        return window;
    }
}
