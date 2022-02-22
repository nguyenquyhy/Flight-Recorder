using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using static FlightRecorder.Client.StateMachine;

namespace FlightRecorder.Client.ViewModels.States
{

    public record Transition
    {
        public static Transition From(State state) => new Transition() with { FromState = state };

        public State FromState { get; init; }
        public State ToState { get; init; }
        public Event ByEvent { get; init; }
        public ImmutableList<object> Actions { get; init; } = ImmutableList<object>.Empty;
        public Event[]? ViaEvents { get; init; }
        public Event[]? WaitForEvents { get; init; }
        public string? RevertErrorMessage { get; set; }

        public Transition To(State state) => this with { ToState = state };

        /// <summary>
        /// Can be called multiple times
        /// </summary>
        public Transition Then(Func<bool> action) => this with { Actions = Actions.Add(action) };

        public Transition Then(Func<Task<bool>> action) => this with { Actions = Actions.Add(action) };

        public Transition By(Event e) => this with { ByEvent = e };

        public Transition Via(params Event[] events) => this with { ViaEvents = events };

        public Transition WaitFor(params Event[] events) => this with { WaitForEvents = events };

        public Transition RevertOnError(string errorMessage) => this with { RevertErrorMessage = errorMessage };

        public async Task<State?> ExecuteAsync()
        {
            if (ViaEvents != null) throw new InvalidOperationException($"This transition from {FromState} by {ByEvent} cannot be executed directly!");

            foreach (var action in Actions)
            {
                switch (action)
                {
                    case Func<bool> syncAction:
                        if (!syncAction())
                            return null;
                        break;
                    case Func<Task<bool>> asyncAction:
                        if (!await asyncAction())
                            return null;
                        break;
                }

            }
            return ToState;
        }
    }
}
