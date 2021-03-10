using System;
using System.Collections.Immutable;
using static FlightRecorder.Client.StateMachine;

namespace FlightRecorder.Client.ViewModels.States
{

    public record Transition
    {
        public static Transition From(State state) => new Transition() with { FromState = state };

        public State FromState { get; init; }
        public State ToState { get; init; }
        public Event ByEvent { get; init; }
        public ImmutableList<Func<bool>> Actions { get; init; } = ImmutableList<Func<bool>>.Empty;
        public Event[] ViaEvents { get; init; }
        public Event[] WaitForEvents { get; init; }

        public Transition To(State state) => this with { ToState = state };

        /// <summary>
        /// Can be called multiple times
        /// </summary>
        public Transition Then(Func<bool> action) => this with { Actions = Actions.Add(action) };

        public Transition By(Event e) => this with { ByEvent = e };

        public Transition Via(params Event[] events) => this with { ViaEvents = events };

        public Transition WaitFor(params Event[] events) => this with { WaitForEvents = events };

        public State? Execute()
        {
            if (ViaEvents != null) throw new InvalidOperationException("This transition cannot be executed directly!");

            foreach (var action in Actions)
            {
                if (!action())
                    return null;
            }
            return ToState;
        }
    }
}
