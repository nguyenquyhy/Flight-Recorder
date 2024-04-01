using System;
using System.Threading.Tasks;

namespace FlightRecorder.Client;

public interface IStateMachine
{
    StateMachine.State CurrentState { get; }

    event EventHandler<StateChangedEventArgs>? StateChanged;

    Task<bool> TransitAsync(StateMachine.Event e, bool fromShortcut = false);
    Task<bool> TransitFromShortcutAsync(StateMachine.Event e);
}