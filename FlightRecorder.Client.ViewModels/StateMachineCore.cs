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
        public event EventHandler<StateChangedEventArgs>? StateChanged;

        protected readonly ILogger logger;
        protected readonly IDialogLogic dialogLogic;
        private readonly MainViewModel viewModel;
        private readonly ConcurrentDictionary<Event, TaskCompletionSource<State>> waitingTasks = new();
        private readonly Dictionary<State, Dictionary<Event, Transition>> stateLogics = new();

        /// <summary>
        /// This is to store the events that trigger during another long running transition,
        /// so that if the long running transition is reverted, we can replay those events.
        /// </summary>
        private List<Event>? transitioningEvents = null;

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
            logger.LogDebug("Triggering event {event} from state {state}", e, CurrentState);

            if (stateLogics.TryGetValue(CurrentState, out var transitions) && transitions.TryGetValue(e, out var transition))
            {
                if (transitioningEvents != null && !waitingTasks.Keys.Contains(e))
                {
                    logger.LogInformation("{event} is triggered from {state} during a multiple transition.", e, CurrentState);
                    transitioningEvents.Add(e);
                }

                if (transition.ViaEvents != null)
                {
                    return await ExecuteMultipleTransitionsAsync(e, transition.ViaEvents, transition.WaitForEvents, transition.RevertErrorMessage);
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
                // TODO: might need to suppress this if waitingTask is applicable
                // to avoid race condition with state setting in the multiple transition
                CurrentState = resultingState.Value;

                StateChanged?.Invoke(this, new StateChangedEventArgs(oldState, resultingState.Value, originatingEvent));
            }
            else
            {
                success = false;
            }

            logger.LogInformation("Triggered event {event} from state {state} to {resultingState}", originatingEvent, oldState, resultingState);

            if (waitingTasks.TryGetValue(originatingEvent, out var waitingTask))
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
        /// <param name="revertErrorMessage">Revert state if there is a an error and show the error message</param>
        protected async Task<bool> ExecuteMultipleTransitionsAsync(Event originatingEvent, Event[] viaEvents, Event[]? waitForEvents, string? revertErrorMessage)
        {
            var success = true;

            var originalState = CurrentState;

            if (transitioningEvents != null)
            {
                logger.LogError("{event} is triggered when another multiple transition is happening!", originatingEvent);
            }

            var localTransitioningEvents = new List<Event>();
            transitioningEvents = localTransitioningEvents;
            try
            {
                // NOTE: we have to initialize all the waiting tasks here to prevent concurrency issue
                // when the waiting event triggered before the waiting task is initialized
                if (waitForEvents != null)
                {
                    foreach (var waitForEvent in waitForEvents)
                    {
                        var tcs = new TaskCompletionSource<State>();
                        waitingTasks.TryAdd(waitForEvent, tcs);
                    }
                }

                foreach (var via in viaEvents)
                {
                    logger.LogDebug("Processing {via} at state {state} due to {event}.", via, CurrentState, originatingEvent);
                    if (waitForEvents != null && waitForEvents.Contains(via))
                    {
                        // This event is completed asynchronously in another thread
                        if (waitingTasks.TryGetValue(via, out var waitingTask))
                        {
                            logger.LogInformation("Waiting for {via} at state {state} due to {event}.", via, CurrentState, originatingEvent);
                            await waitingTask.Task;
                            logger.LogInformation("Finished waiting for {via} at state {state} due to {event}.", via, CurrentState, originatingEvent);

                            waitingTasks.Remove(via, out _);
                        }
                        else
                        {
                            throw new InvalidOperationException($"Cannot find Task for {via}!");
                        }
                    }
                    else
                    {
                        // TODO: maybe recurse here?

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
                            if (revertErrorMessage != null)
                            {
                                logger.LogInformation("Transition from {state} by {via} was cancelled! Revert back to orignal state {original}.", oldState, via, originalState);
                                await RevertStateAsync(originalState, originatingEvent, localTransitioningEvents);
                            }

                            break;
                        }
                    }
                }
            }
            catch (Exception ex) when (revertErrorMessage != null)
            {
                logger.LogError(ex, "Cannot complete the transition from {state} by {event}! Revert back to orignal state.", originalState, originatingEvent);
                await RevertStateAsync(originalState, originatingEvent, localTransitioningEvents);
                dialogLogic.Error(revertErrorMessage);
            }
            finally
            {
                transitioningEvents = null;
            }

            return success;
        }

        protected async Task RevertStateAsync(State originalState, Event originatingEvent, List<Event> localTransitioningEvents)
        {
            logger.LogInformation("Reverting to {originalState} from transition by {originatingEvent}", originalState, originatingEvent);

            var oldState = CurrentState;
            CurrentState = originalState;
            StateChanged?.Invoke(this, new StateChangedEventArgs(oldState, originalState, originatingEvent));
            viewModel.State = originalState;

            if (localTransitioningEvents != null)
            {
                transitioningEvents = null;
                foreach (var e in localTransitioningEvents)
                {
                    logger.LogInformation("Replay {event} from {state}", e, CurrentState);
                    await TransitAsync(e);
                }
            }
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
