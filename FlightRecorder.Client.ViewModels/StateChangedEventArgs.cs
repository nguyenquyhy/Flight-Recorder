using System;
using static FlightRecorder.Client.StateMachine;

namespace FlightRecorder.Client
{
    public class StateChangedEventArgs : EventArgs
    {
        public StateChangedEventArgs(State from, State to, Event by)
        {
            From = from;
            To = to;
            By = by;
        }

        public State From { get; }
        public State To { get; }
        public Event By { get; }
    }
}
