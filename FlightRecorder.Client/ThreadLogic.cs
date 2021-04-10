using System;
using System.Windows;

namespace FlightRecorder.Client
{
    public class ThreadLogic : IThreadLogic
    {
        private Window window = null;

        public void Register(Window window) => this.window = window;

        public void RunInUIThread(Action action)
        {
            window?.Dispatcher.Invoke(action);
        }
    }
}
