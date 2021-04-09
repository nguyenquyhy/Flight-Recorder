using FlightRecorder.Client.Logics;
using FlightRecorder.Client.ViewModels.States;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FlightRecorder.Client
{
    public class StateMachine
    {
        public const string LoadErrorMessage = "The selected file is not a valid recording or not accessible!\n\nAre you sure you are opening a *.flightrecorder file?";
        public const string SaveErrorMessage = "Flight Recorder cannot write the file to disk.\nPlease make sure the folder is accessible by Flight Recorder, and you are not overwriting a locked file.";

        public event EventHandler<StateChangedEventArgs> StateChanged;

        public State CurrentState { get; private set; } = State.Start;

        private readonly ILogger<StateMachine> logger;
        private readonly MainViewModel viewModel;
        private readonly IRecorderLogic recorderLogic;
        private readonly IDialogLogic dialogLogic;
        public readonly string currentVersion;

        private readonly Dictionary<State, Dictionary<Event, Transition>> stateLogics = new();

        public enum Event
        {
            StartUp,
            Connect,
            Disconnect,
            Record,
            Replay,
            Pause,
            Resume,
            RequestStopping,
            Stop,
            RequestSaving,
            RequestLoading,
            Save,
            Load,
            Exit
        }

        public enum State
        {
            Start,
            DisconnectedEmpty,
            DisconnectedUnsaved,
            DisconnectedSaved,
            IdleEmpty,
            IdleUnsaved,
            IdleSaved,
            SavingDisconnected,
            SavingIdle,
            LoadingDisconnected,
            LoadingIdle,
            Recording,
            ReplayingUnsaved,
            PausingUnsaved,
            ReplayingSaved,
            PausingSaved,
            End
        }

        public StateMachine(ILogger<StateMachine> logger, MainViewModel viewModel, IRecorderLogic recorderLogic, IDialogLogic dialogLogic)
        {
            this.logger = logger;
            this.viewModel = viewModel;
            this.recorderLogic = recorderLogic;
            this.dialogLogic = dialogLogic;
            this.currentVersion = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();

            InitializeStateMachine();
        }

        private void InitializeStateMachine()
        {
            Register(Transition.From(State.Start).To(State.DisconnectedEmpty).By(Event.StartUp).ThenUpdate(viewModel));

            Register(Transition.From(State.DisconnectedEmpty).To(State.IdleEmpty).By(Event.Connect).Then(Connect).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedEmpty).To(State.LoadingDisconnected).By(Event.RequestLoading).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedEmpty).To(State.End).By(Event.Exit)); // NO-OP
            Register(Transition.From(State.DisconnectedEmpty).To(State.DisconnectedEmpty).By(Event.Disconnect)); // NO-OP

            Register(Transition.From(State.DisconnectedSaved).To(State.IdleSaved).By(Event.Connect).Then(Connect).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedSaved).To(State.SavingDisconnected).By(Event.RequestSaving).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedSaved).To(State.LoadingDisconnected).By(Event.RequestLoading).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedSaved).To(State.End).By(Event.Exit)); // NO-OP
            Register(Transition.From(State.DisconnectedSaved).To(State.DisconnectedSaved).By(Event.Disconnect)); // NO-OP

            Register(Transition.From(State.DisconnectedUnsaved).To(State.IdleUnsaved).By(Event.Connect).Then(Connect).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedUnsaved).To(State.SavingDisconnected).By(Event.RequestSaving).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedUnsaved).To(State.LoadingDisconnected).By(Event.RequestLoading).Then(() =>
            {
                return dialogLogic.Confirm("You haven't saved the recording.\nLoading a recording will remove current one.\nDo you want to proceed?");
            }).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedUnsaved).To(State.End).By(Event.Exit).Then(() =>
            {
                return dialogLogic.Confirm("You haven't saved the recording.\n\nDo you want to exit Flight Recorder without saving?");
            }));
            Register(Transition.From(State.DisconnectedUnsaved).To(State.DisconnectedUnsaved).By(Event.Disconnect)); // NO-OP

            Register(Transition.From(State.IdleEmpty).To(State.DisconnectedEmpty).By(Event.Disconnect).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleEmpty).To(State.Recording).By(Event.Record).Then(StartRecording).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleEmpty).To(State.LoadingIdle).By(Event.RequestLoading).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleEmpty).To(State.End).By(Event.Exit));

            Register(Transition.From(State.IdleSaved).To(State.DisconnectedSaved).By(Event.Disconnect).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleSaved).To(State.Recording).By(Event.Record).Then(StartRecording).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleSaved).To(State.ReplayingSaved).By(Event.Replay).Then(recorderLogic.Replay).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleSaved).To(State.SavingIdle).By(Event.RequestSaving).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleSaved).To(State.LoadingIdle).By(Event.RequestLoading).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleSaved).To(State.End).By(Event.Exit));

            Register(Transition.From(State.IdleUnsaved).To(State.DisconnectedUnsaved).By(Event.Disconnect).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleUnsaved).To(State.Recording).By(Event.Record).Then(() =>
            {
                return dialogLogic.Confirm("You haven't saved the recording.\nStarting a new recording will remove current one.\nDo you want to proceed?");
            }).Then(StartRecording).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleUnsaved).To(State.ReplayingUnsaved).By(Event.Replay).Then(recorderLogic.Replay).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleUnsaved).To(State.SavingIdle).By(Event.RequestSaving).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleUnsaved).To(State.LoadingIdle).By(Event.RequestLoading).Then(() =>
            {
                return dialogLogic.Confirm("You haven't saved the recording.\nLoading a recording will remove current one.\nDo you want to proceed?");
            }).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleUnsaved).To(State.End).By(Event.Exit).Then(() =>
            {
                return dialogLogic.Confirm("You haven't saved the recording.\n\nDo you want to exit Flight Recorder without saving?");
            }));

            Register(Transition.From(State.SavingIdle).To(State.IdleSaved).By(Event.Save).Then(SaveRecordingAsync).ThenUpdate(viewModel));
            Register(Transition.From(State.SavingIdle).To(State.SavingDisconnected).By(Event.Disconnect).ThenUpdate(viewModel));
            Register(Transition.From(State.SavingIdle).To(State.SavingIdle).By(Event.Exit)); // NO-OP
            Register(Transition.From(State.SavingDisconnected).To(State.DisconnectedSaved).By(Event.Save).Then(SaveRecordingAsync).ThenUpdate(viewModel));
            Register(Transition.From(State.SavingDisconnected).To(State.SavingIdle).By(Event.Connect).ThenUpdate(viewModel));
            Register(Transition.From(State.SavingDisconnected).To(State.SavingDisconnected).By(Event.Disconnect)); // NO-OP
            Register(Transition.From(State.SavingDisconnected).To(State.SavingDisconnected).By(Event.Exit)); // NO-OP

            Register(Transition.From(State.LoadingIdle).To(State.IdleSaved).By(Event.Load).Then(LoadRecordingAsync).ThenUpdate(viewModel));
            Register(Transition.From(State.LoadingIdle).To(State.LoadingDisconnected).By(Event.Disconnect).ThenUpdate(viewModel));
            Register(Transition.From(State.LoadingIdle).To(State.LoadingIdle).By(Event.Exit)); // NO-OP
            Register(Transition.From(State.LoadingDisconnected).To(State.DisconnectedSaved).By(Event.Load).Then(LoadRecordingAsync).ThenUpdate(viewModel));
            Register(Transition.From(State.LoadingDisconnected).To(State.LoadingIdle).By(Event.Connect).ThenUpdate(viewModel));
            Register(Transition.From(State.LoadingDisconnected).To(State.LoadingDisconnected).By(Event.Disconnect)); // NO-OP
            Register(Transition.From(State.LoadingDisconnected).To(State.LoadingDisconnected).By(Event.Exit)); // NO-OP

            Register(Transition.From(State.Recording).To(State.IdleUnsaved).By(Event.Stop).Then(() => { recorderLogic.StopRecording(); return true; }).ThenUpdate(viewModel));

            Register(Transition.From(State.ReplayingSaved).To(State.ReplayingSaved).By(Event.RequestStopping).Then(RequestStopping));
            Register(Transition.From(State.ReplayingSaved).To(State.PausingSaved).By(Event.Pause).Then(() => recorderLogic.PauseReplay()).ThenUpdate(viewModel));
            Register(Transition.From(State.ReplayingSaved).To(State.IdleSaved).By(Event.Stop).Then(StopReplay).ThenUpdate(viewModel));

            Register(Transition.From(State.ReplayingUnsaved).To(State.ReplayingUnsaved).By(Event.RequestStopping).Then(RequestStopping));
            Register(Transition.From(State.ReplayingUnsaved).To(State.PausingUnsaved).By(Event.Pause).Then(() => recorderLogic.PauseReplay()).ThenUpdate(viewModel));
            Register(Transition.From(State.ReplayingUnsaved).To(State.IdleUnsaved).By(Event.Stop).Then(StopReplay).ThenUpdate(viewModel));

            Register(Transition.From(State.PausingSaved).To(State.PausingSaved).By(Event.RequestStopping).Then(RequestStopping));
            Register(Transition.From(State.PausingSaved).To(State.ReplayingSaved).By(Event.Resume).Then(() => recorderLogic.ResumeReplay()).ThenUpdate(viewModel));
            Register(Transition.From(State.PausingSaved).To(State.IdleSaved).By(Event.Stop).Then(StopReplay).ThenUpdate(viewModel));

            Register(Transition.From(State.PausingUnsaved).To(State.PausingUnsaved).By(Event.RequestStopping).Then(RequestStopping));
            Register(Transition.From(State.PausingUnsaved).To(State.ReplayingUnsaved).By(Event.Resume).Then(() => recorderLogic.ResumeReplay()).ThenUpdate(viewModel));
            Register(Transition.From(State.PausingUnsaved).To(State.IdleUnsaved).By(Event.Stop).Then(StopReplay).ThenUpdate(viewModel));


            // Shortcut path
            Register(Transition.From(State.DisconnectedEmpty).By(Event.Load).Via(Event.RequestLoading, Event.Load).RevertOnError(LoadErrorMessage));
            Register(Transition.From(State.DisconnectedUnsaved).By(Event.Load).Via(Event.RequestLoading, Event.Load).RevertOnError(LoadErrorMessage));
            Register(Transition.From(State.DisconnectedSaved).By(Event.Load).Via(Event.RequestLoading, Event.Load).RevertOnError(LoadErrorMessage));
            Register(Transition.From(State.IdleEmpty).By(Event.Load).Via(Event.RequestLoading, Event.Load).RevertOnError(LoadErrorMessage));
            Register(Transition.From(State.IdleUnsaved).By(Event.Load).Via(Event.RequestLoading, Event.Load).RevertOnError(LoadErrorMessage));
            Register(Transition.From(State.IdleSaved).By(Event.Load).Via(Event.RequestLoading, Event.Load).RevertOnError(LoadErrorMessage));

            Register(Transition.From(State.DisconnectedUnsaved).By(Event.Save).Via(Event.RequestSaving, Event.Save).RevertOnError(SaveErrorMessage));
            Register(Transition.From(State.DisconnectedSaved).By(Event.Save).Via(Event.RequestSaving, Event.Save).RevertOnError(SaveErrorMessage));
            Register(Transition.From(State.IdleUnsaved).By(Event.Save).Via(Event.RequestSaving, Event.Save).RevertOnError(SaveErrorMessage));
            Register(Transition.From(State.IdleSaved).By(Event.Save).Via(Event.RequestSaving, Event.Save).RevertOnError(SaveErrorMessage));

            Register(Transition.From(State.Recording).By(Event.Disconnect).Via(Event.Stop, Event.Disconnect));
            Register(Transition.From(State.ReplayingSaved).By(Event.Disconnect).Via(Event.RequestStopping, Event.Stop, Event.Disconnect).WaitFor(Event.Stop));
            Register(Transition.From(State.ReplayingUnsaved).By(Event.Disconnect).Via(Event.RequestStopping, Event.Stop, Event.Disconnect).WaitFor(Event.Stop));
            Register(Transition.From(State.PausingSaved).By(Event.Disconnect).Via(Event.RequestStopping, Event.Stop, Event.Disconnect).WaitFor(Event.Stop));
            Register(Transition.From(State.PausingUnsaved).By(Event.Disconnect).Via(Event.RequestStopping, Event.Stop, Event.Disconnect).WaitFor(Event.Stop));

            Register(Transition.From(State.Recording).By(Event.Exit).Via(Event.Stop, Event.Exit));
            Register(Transition.From(State.ReplayingSaved).By(Event.Exit).Via(Event.RequestStopping, Event.Stop, Event.Exit).WaitFor(Event.Stop));
            Register(Transition.From(State.ReplayingUnsaved).By(Event.Exit).Via(Event.RequestStopping, Event.Stop, Event.Exit).WaitFor(Event.Stop));
            Register(Transition.From(State.PausingSaved).By(Event.Exit).Via(Event.RequestStopping, Event.Stop, Event.Exit).WaitFor(Event.Stop));
            Register(Transition.From(State.PausingUnsaved).By(Event.Exit).Via(Event.RequestStopping, Event.Stop, Event.Exit).WaitFor(Event.Stop));
        }

        private bool Connect()
        {
            viewModel.SimConnectState = SimConnectState.Connected;
            recorderLogic.Initialize();
            return true;
        }

        private bool StartRecording()
        {
            recorderLogic.Record();
            viewModel.CurrentFrame = 0;
            return true;
        }
        private Task<bool> SaveRecordingAsync() => dialogLogic.SaveAsync(recorderLogic.ToData(currentVersion));

        private async Task<bool> LoadRecordingAsync()
        {
            var data = await dialogLogic.LoadAsync();
            if (data != null)
            {
                recorderLogic.FromData(data);
                return true;
            }
            return false;
        }

        private bool RequestStopping()
        {
            recorderLogic.StopReplay();
            return true;
        }

        private bool StopReplay()
        {
            viewModel.CurrentFrame = 0;
            return true;
        }

        private readonly ConcurrentDictionary<Event, TaskCompletionSource<State>> waitingTasks = new();

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

        private async Task<bool> ExecuteSingleTransitionAsync(Event originatingEvent, Transition transition)
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
        private async Task<bool> ExecuteMultipleTransitionsAsync(Event originatingEvent, Event[] viaEvents, Event[] waitForEvents, bool revertOnError, string errorMessage)
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

        private void RevertState(State originalState, Event originatingEvent)
        {
            var oldState = CurrentState;
            CurrentState = originalState;
            StateChanged?.Invoke(this, new StateChangedEventArgs(oldState, originalState, originatingEvent));
            viewModel.State = originalState;
        }

        private void Register(Transition logic)
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
