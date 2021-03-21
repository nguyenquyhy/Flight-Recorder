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
        public event EventHandler<StateChangedEventArgs> StateChanged;

        public State CurrentState { get; private set; } = State.Start;

        private readonly ILogger<StateMachine> logger;
        private readonly MainViewModel viewModel;
        private readonly IRecorderLogic recorderLogic;
        private readonly IDialogLogic dialogLogic;

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
            InitializeStateMachine();
        }

        private void InitializeStateMachine()
        {
            Register(Transition.From(State.Start).To(State.DisconnectedEmpty).By(Event.StartUp).ThenUpdate(viewModel));

            Register(Transition.From(State.DisconnectedEmpty).To(State.IdleEmpty).By(Event.Connect).Then(Connect).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedEmpty).To(State.DisconnectedSaved).By(Event.Load).Then(LoadRecording).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedEmpty).To(State.End).By(Event.Exit)); // NO-OP
            Register(Transition.From(State.DisconnectedEmpty).To(State.DisconnectedEmpty).By(Event.Disconnect)); // NO-OP

            Register(Transition.From(State.DisconnectedSaved).To(State.IdleSaved).By(Event.Connect).Then(Connect).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedSaved).To(State.DisconnectedSaved).By(Event.Save).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedSaved).To(State.DisconnectedSaved).By(Event.Load).Then(LoadRecording).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedSaved).To(State.End).By(Event.Exit)); // NO-OP
            Register(Transition.From(State.DisconnectedSaved).To(State.DisconnectedSaved).By(Event.Disconnect)); // NO-OP

            Register(Transition.From(State.DisconnectedUnsaved).To(State.IdleUnsaved).By(Event.Connect).Then(Connect).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedUnsaved).To(State.DisconnectedSaved).By(Event.Save).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedUnsaved).To(State.DisconnectedSaved).By(Event.Load).Then(() =>
            {
                return dialogLogic.Confirm("You haven't saved the recording.\nLoading a recording will remove current one.\nDo you want to proceed?");
            }).Then(LoadRecording).ThenUpdate(viewModel));
            Register(Transition.From(State.DisconnectedUnsaved).To(State.End).By(Event.Exit).Then(() =>
            {
                return dialogLogic.Confirm("You haven't saved the recording.\n\nDo you want to exit Flight Recorder without saving?");
            }));
            Register(Transition.From(State.DisconnectedUnsaved).To(State.DisconnectedUnsaved).By(Event.Disconnect)); // NO-OP

            Register(Transition.From(State.IdleEmpty).To(State.DisconnectedEmpty).By(Event.Disconnect).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleEmpty).To(State.Recording).By(Event.Record).Then(StartRecording).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleEmpty).To(State.IdleSaved).By(Event.Load).Then(LoadRecording).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleEmpty).To(State.End).By(Event.Exit));

            Register(Transition.From(State.IdleSaved).To(State.DisconnectedSaved).By(Event.Disconnect).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleSaved).To(State.Recording).By(Event.Record).Then(StartRecording).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleSaved).To(State.ReplayingSaved).By(Event.Replay).Then(recorderLogic.Replay).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleSaved).To(State.IdleSaved).By(Event.Save).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleSaved).To(State.IdleSaved).By(Event.Load).Then(LoadRecording).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleSaved).To(State.End).By(Event.Exit));

            Register(Transition.From(State.IdleUnsaved).To(State.DisconnectedUnsaved).By(Event.Disconnect).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleUnsaved).To(State.Recording).By(Event.Record).Then(() =>
            {
                return dialogLogic.Confirm("You haven't saved the recording.\nStarting a new recording will remove current one.\nDo you want to proceed?");
            }).Then(StartRecording).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleUnsaved).To(State.ReplayingUnsaved).By(Event.Replay).Then(recorderLogic.Replay).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleUnsaved).To(State.IdleSaved).By(Event.Save).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleUnsaved).To(State.IdleSaved).By(Event.Load).Then(() =>
            {
                return dialogLogic.Confirm("You haven't saved the recording.\nLoading a recording will remove current one.\nDo you want to proceed?");
            }).Then(LoadRecording).ThenUpdate(viewModel));
            Register(Transition.From(State.IdleUnsaved).To(State.End).By(Event.Exit).Then(() =>
            {
                return dialogLogic.Confirm("You haven't saved the recording.\n\nDo you want to exit Flight Recorder without saving?");
            }));

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
            Register(Transition.From(State.Recording).To(State.DisconnectedUnsaved).By(Event.Disconnect).Via(Event.Stop, Event.Disconnect));
            Register(Transition.From(State.ReplayingSaved).To(State.DisconnectedSaved).By(Event.Disconnect).Via(Event.RequestStopping, Event.Stop, Event.Disconnect).WaitFor(Event.Stop));
            Register(Transition.From(State.ReplayingUnsaved).To(State.DisconnectedUnsaved).By(Event.Disconnect).Via(Event.RequestStopping, Event.Stop, Event.Disconnect).WaitFor(Event.Stop));
            Register(Transition.From(State.PausingSaved).To(State.DisconnectedSaved).By(Event.Disconnect).Via(Event.RequestStopping, Event.Stop, Event.Disconnect).WaitFor(Event.Stop));
            Register(Transition.From(State.PausingUnsaved).To(State.DisconnectedUnsaved).By(Event.Disconnect).Via(Event.RequestStopping, Event.Stop, Event.Disconnect).WaitFor(Event.Stop));

            Register(Transition.From(State.Recording).To(State.End).By(Event.Exit).Via(Event.Stop, Event.Exit));
            Register(Transition.From(State.ReplayingSaved).To(State.End).By(Event.Exit).Via(Event.RequestStopping, Event.Stop, Event.Exit).WaitFor(Event.Stop));
            Register(Transition.From(State.ReplayingUnsaved).To(State.End).By(Event.Exit).Via(Event.RequestStopping, Event.Stop, Event.Exit).WaitFor(Event.Stop));
            Register(Transition.From(State.PausingSaved).To(State.End).By(Event.Exit).Via(Event.RequestStopping, Event.Stop, Event.Exit).WaitFor(Event.Stop));
            Register(Transition.From(State.PausingUnsaved).To(State.End).By(Event.Exit).Via(Event.RequestStopping, Event.Stop, Event.Exit).WaitFor(Event.Stop));
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

        private bool LoadRecording()
        {
            try
            {
                var data = dialogLogic.Load();
                if (data != null)
                {
                    recorderLogic.FromData(data);
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cannot load file");
                dialogLogic.Error("The selected file is not a valid recording or not accessible!\n\nAre you sure you are opening a *.flightrecorder file?");
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

        public async Task<bool> TransitAsync(Event e)
        {
            logger.LogTrace("Triggering event {event} from state {state}", e, CurrentState);

            var success = true;

            if (stateLogics.TryGetValue(CurrentState, out var transitions) && transitions.TryGetValue(e, out var transition))
            {
                if (transition.ViaEvents != null)
                {
                    foreach (var via in transition.ViaEvents)
                    {
                        // TODO: maybe recurse here?

                        if (transition.WaitForEvents != null && transition.WaitForEvents.Contains(via))
                        {
                            var tcs = new TaskCompletionSource<State>();
                            waitingTasks.TryAdd(via, tcs);
                            await tcs.Task;

                            // This event is completed asynchronously in another thread, so it doesn't need to trigger here
                        }
                        else
                        {
                            var oldState = CurrentState;
                            var resultingState = stateLogics[oldState][via].Execute();
                            if (resultingState.HasValue)
                            {
                                CurrentState = resultingState.Value;

                                StateChanged?.Invoke(this, new StateChangedEventArgs(oldState, resultingState.Value, via));
                            }
                            else
                            {
                                success = false;
                            }
                            logger.LogInformation("Triggered event {via} due to {event} from state {state} to {resultingState}", via, e, oldState, resultingState);

                            if (!success)
                            {
                                break;
                            }
                        }
                    }
                }
                else
                {
                    var oldState = CurrentState;
                    var resultingState = transition.Execute();

                    if (resultingState.HasValue)
                    {
                        CurrentState = resultingState.Value;

                        StateChanged?.Invoke(this, new StateChangedEventArgs(oldState, resultingState.Value, e));
                    }
                    else
                    {
                        success = false;
                    }
                    logger.LogInformation("Triggered event {event} from state {state} to {resultingState}", e, oldState, resultingState);

                    if (waitingTasks.TryRemove(e, out var waitingTask))
                    {
                        waitingTask.SetResult(CurrentState);
                    }
                }

                return success;
            }
            else
            {
                logger.LogError("Cannot trigger {event} from {state}", e, CurrentState);
                throw new InvalidOperationException($"Cannot trigger {e} from {CurrentState}!");
            }
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
