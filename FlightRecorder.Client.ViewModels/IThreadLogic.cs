using System;

namespace FlightRecorder.Client
{
    public interface IThreadLogic
    {
        void RunInUIThread(Action action);
    }
}
