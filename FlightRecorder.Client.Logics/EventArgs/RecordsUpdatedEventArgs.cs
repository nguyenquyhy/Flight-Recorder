using System;

namespace FlightRecorder.Client.Logics
{
    public class RecordsUpdatedEventArgs : EventArgs
    {
        public int Count { get; }

        public RecordsUpdatedEventArgs(int count)
        {
            Count = count;
        }
    }
}
