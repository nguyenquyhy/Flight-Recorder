using FlightRecorder.Client.ViewModels.States;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static FlightRecorder.Client.StateMachine;

namespace FlightRecorder.Client
{
    public abstract class StateMachineCore
    {
        public event EventHandler<StateChangedEventArgs> StateChanged;

        protected readonly ILogger logger;
        protected readonly IDialogLogic dialogLogic;
        private readonly MainViewModel viewModel;
        private readonly ConcurrentDictionary<Event, TaskCompletionSource<State>> waitingTasks = new();
        private readonly Dictionary<State, Dictionary<Event, Transition>> stateLogics = new();

        public State CurrentState { get; private set; } = State.Start;

        public StateMachineCore(ILogger logger, IDialogLogic dialogLogic, MainViewModel viewModel)
        {
            this.logger = logger;
            this.dialogLogic = dialogLogic;
            this.viewModel = viewModel;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns>True to indicate that user did not cancel any prompt</returns>
        public async Task<bool> TransitAsync(Event e)
        {
            logger.LogTrace("Triggering event {event} from state {state}", e, CurrentState);

            if (stateLogics.TryGetValue(CurrentState, out var transitions) && transitions.TryGetValue(e, out var transition))
            {
                if (transition.ViaEvents != null)
                {
                    return await ExecuteMultipleTransitionsAsync(e, transition.ViaEvents, transition.WaitForEvents, transition.ShouldRevertOnError, transition.ErrorMessage);
                }
                else
                {
                    return await ExecuteSingleTransitionAsync(e, transition);
                }
            }
            else
            {
                logger.LogError("Cannot trigger {event} from {state}", e, CurrentState);
                throw new InvalidOperationException($"Cannot trigger {e} from {CurrentState}!");
            }
        }

        protected async Task<bool> ExecuteSingleTransitionAsync(Event originatingEvent, Transition transition)
        {
            var oldState = CurrentState;
            var resultingState = await transition.ExecuteAsync();

            var success = true;

            if (resultingState.HasValue)
            {
                CurrentState = resultingState.Value;

                StateChanged?.Invoke(this, new StateChangedEventArgs(oldState, resultingState.Value, originatingEvent));
            }
            else
            {
                success = false;
            }

            logger.LogInformation("Triggered event {event} from state {state} to {resultingState}", originatingEvent, oldState, resultingState);

            if (waitingTasks.TryRemove(originatingEvent, out var waitingTask))
            {
                waitingTask.SetResult(CurrentState);
            }

            return success;
        }

        /// <summary>
        /// Handle the case when a single event should be resolved into multiple events to leverage other existing transition.
        /// </summary>
        /// <param name="originatingEvent">The event that is triggered externally</param>
        /// <param name="viaEvents">The events that state machine should triggered instead</param>
        /// <param name="waitForEvents">Some events in the viaEvents list should not be triggered by the state machine itself. Instead, the state machine should wait for the event to be triggered externally before continue with the viaEvents list.</param>
        /// <returns></returns>
        protected async Task<bool> ExecuteMultipleTransitionsAsync(Event originatingEvent, Event[] viaEvents, Event[] waitForEvents, bool revertOnError, string errorMessage)
        {
            var success = true;

            var originalState = CurrentState;

            try
            {
                foreach (var via in viaEvents)
                {
                    // TODO: maybe recurse here?

                    if (waitForEvents != null && waitForEvents.Contains(via))
                    {
                        var tcs = new TaskCompletionSource<State>();
                        waitingTasks.TryAdd(via, tcs);
                        await tcs.Task;

                        // This event is completed asynchronously in another thread, so it doesn't need to trigger here
                    }
                    else
                    {
                        var oldState = CurrentState;
                        var resultingState = await stateLogics[oldState][via].ExecuteAsync();
                        if (resultingState.HasValue)
                        {
                            CurrentState = resultingState.Value;

                            StateChanged?.Invoke(this, new StateChangedEventArgs(oldState, resultingState.Value, via));
                        }
                        else
                        {
                            success = false;
                        }
                        logger.LogInformation("Triggered event {via} due to {event} from state {state} to {resultingState}", via, originatingEvent, oldState, resultingState);

                        if (!success)
                        {
                            if (revertOnError)
                            {
                                logger.LogInformation("Transition from {state} by {via} was cancelled! Revert back to orignal state {original}.", oldState, via, originalState);
                                RevertState(originalState, originatingEvent);
                            }

                            break;
                        }
                    }
                }
            }
            catch (Exception ex) when (revertOnError)
            {
                logger.LogError(ex, "Cannot complete the transition from {state} by {event}! Revert back to orignal state.", originalState, originatingEvent);
                RevertState(originalState, originatingEvent);
                dialogLogic.Error(errorMessage);
            }

            return success;
        }

        protected void RevertState(State originalState, Event originatingEvent)
        {
            var oldState = CurrentState;
            CurrentState = originalState;
            StateChanged?.Invoke(this, new StateChangedEventArgs(oldState, originalState, originatingEvent));
            viewModel.State = originalState;
        }

        protected void Register(Transition logic)
        {
            if (!stateLogics.ContainsKey(logic.FromState))
            {
                stateLogics.Add(logic.FromState, new());
            }
            var fromLogic = stateLogics[logic.FromState];
            if (fromLogic.ContainsKey(logic.ByEvent))
            {
                throw new InvalidOperationException($"There is already a transition from {logic.FromState} that use {logic.ByEvent} (to {fromLogic[logic.ByEvent].ToState})!");
            }
            fromLogic.Add(logic.ByEvent, logic);
        }
    }
}
