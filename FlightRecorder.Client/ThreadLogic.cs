using System;
using System.Windows;

namespace FlightRecorder.Client
{
    public class ThreadLogic : IThreadLogic
    {
        private Window? window = null;

        public void Register(object window) => this.window = window as Window ?? throw new ArgumentException("Window object is required!", nameof(window));

        public void RunInUIThread(Action action)
        {
            window?.Dispatcher.Invoke(action);
        }
    }
}
