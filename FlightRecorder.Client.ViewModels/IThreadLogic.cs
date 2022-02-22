using System;

namespace FlightRecorder.Client
{
    public interface IThreadLogic
    {
        void Register(object window);
        void RunInUIThread(Action action);
    }
}
