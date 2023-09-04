using FlightRecorder.Client.Logics;
using FlightRecorder.Client.ViewModels.States;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FlightRecorder.Client;

public class StateMachine : StateMachineCore
{
    public const string LoadErrorMessage = "The selected file is not a valid recording or not accessible!\n\nAre you sure you are opening a *.flightrecorder file?";
    public const string SaveErrorMessage = "Flight Recorder cannot write the file to disk.\nPlease make sure the folder is accessible by Flight Recorder, and you are not overwriting a locked file.";

    private readonly MainViewModel viewModel;
    private readonly ISettingsLogic settingsLogic;
    private readonly IStorageLogic storageLogic;
    private readonly IRecorderLogic recorderLogic;
    private readonly IReplayLogic replayLogic;
    private readonly string currentVersion;

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
        LoadAI,
        TrimStart,
        TrimEnd,
        RestoreCrashData,
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

    public StateMachine(
        ILogger<StateMachine> logger,
        MainViewModel viewModel,
        ISettingsLogic settingsLogic,
        IStorageLogic storageLogic,
        IRecorderLogic recorderLogic,
        IReplayLogic replayLogic,
        IDialogLogic dialogLogic,
        VersionLogic versionLogic
    ) : base(logger, dialogLogic, viewModel)
    {
        logger.LogDebug("Creating instance of {class}", nameof(StateMachine));
        this.viewModel = viewModel;
        this.settingsLogic = settingsLogic;
        this.storageLogic = storageLogic;
        this.recorderLogic = recorderLogic;
        this.replayLogic = replayLogic;
        this.currentVersion = versionLogic.GetVersion();

        InitializeStateMachine();
    }

    private void InitializeStateMachine()
    {
        Register(Transition.From(State.Start).To(State.DisconnectedEmpty).By(Event.StartUp).ThenUpdate(viewModel));

        Register(Transition.From(State.DisconnectedEmpty).To(State.DisconnectedUnsaved).By(Event.RestoreCrashData).ThenUpdate(viewModel));

        Register(Transition.From(State.DisconnectedEmpty).To(State.IdleEmpty).By(Event.Connect).Then(Connect).ThenUpdate(viewModel));
        Register(Transition.From(State.DisconnectedEmpty).To(State.LoadingDisconnected).By(Event.RequestLoading).ThenUpdate(viewModel));
        Register(Transition.From(State.DisconnectedEmpty).To(State.End).By(Event.Exit)); // NO-OP
        Register(Transition.From(State.DisconnectedEmpty).To(State.DisconnectedEmpty).By(Event.Disconnect)); // NO-OP
        Register(Transition.From(State.DisconnectedEmpty).To(State.DisconnectedSaved).By(Event.LoadAI).ThenUpdate(viewModel)); // AI

        Register(Transition.From(State.DisconnectedSaved).To(State.IdleSaved).By(Event.Connect).Then(Connect).ThenUpdate(viewModel));
        Register(Transition.From(State.DisconnectedSaved).To(State.SavingDisconnected).By(Event.RequestSaving).ThenUpdate(viewModel));
        Register(Transition.From(State.DisconnectedSaved).To(State.LoadingDisconnected).By(Event.RequestLoading).ThenUpdate(viewModel));
        Register(Transition.From(State.DisconnectedSaved).To(State.DisconnectedUnsaved).By(Event.TrimStart).Then(TrimStart).ThenUpdate(viewModel));
        Register(Transition.From(State.DisconnectedSaved).To(State.DisconnectedUnsaved).By(Event.TrimEnd).Then(TrimEnd).ThenUpdate(viewModel));
        Register(Transition.From(State.DisconnectedSaved).To(State.End).By(Event.Exit)); // NO-OP
        Register(Transition.From(State.DisconnectedSaved).To(State.DisconnectedSaved).By(Event.Disconnect)); // NO-OP

        Register(Transition.From(State.DisconnectedUnsaved).To(State.IdleUnsaved).By(Event.Connect).Then(Connect).ThenUpdate(viewModel));
        Register(Transition.From(State.DisconnectedUnsaved).To(State.SavingDisconnected).By(Event.RequestSaving).ThenUpdate(viewModel));
        Register(Transition.From(State.DisconnectedUnsaved).To(State.LoadingDisconnected).By(Event.RequestLoading).Then(() =>
        {
            return dialogLogic.Confirm("You haven't saved the recording.\nLoading a recording will remove current one.\nDo you want to proceed?");
        }).ThenUpdate(viewModel));
        Register(Transition.From(State.DisconnectedUnsaved).To(State.DisconnectedUnsaved).By(Event.TrimStart).Then(() =>
        {
            return dialogLogic.Confirm("You haven't saved the recording.\nTrimming will remove all frames before the current one.\nDo you want to proceed?");
        }).Then(TrimStart).ThenUpdate(viewModel));
        Register(Transition.From(State.DisconnectedUnsaved).To(State.DisconnectedUnsaved).By(Event.TrimEnd).Then(() =>
        {
            return dialogLogic.Confirm("You haven't saved the recording.\nTrimming will remove all frames after the current one.\nDo you want to proceed?");
        }).Then(TrimEnd).ThenUpdate(viewModel));
        Register(Transition.From(State.DisconnectedUnsaved).To(State.End).By(Event.Exit).Then(() =>
        {
            return dialogLogic.Confirm("You haven't saved the recording.\n\nDo you want to exit Flight Recorder without saving?");
        }));
        Register(Transition.From(State.DisconnectedUnsaved).To(State.DisconnectedUnsaved).By(Event.Disconnect)); // NO-OP

        Register(Transition.From(State.IdleEmpty).To(State.DisconnectedEmpty).By(Event.Disconnect).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleEmpty).To(State.Recording).By(Event.Record).Then(StartRecording).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleEmpty).To(State.LoadingIdle).By(Event.RequestLoading).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleEmpty).To(State.End).By(Event.Exit));
        Register(Transition.From(State.IdleEmpty).To(State.IdleSaved).By(Event.LoadAI).ThenUpdate(viewModel)); // AI
        Register(Transition.From(State.IdleEmpty).To(State.IdleEmpty).By(Event.Connect)); // NO-OP

        Register(Transition.From(State.IdleSaved).To(State.DisconnectedSaved).By(Event.Disconnect).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleSaved).To(State.Recording).By(Event.Record).Then(StartRecording).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleSaved).To(State.ReplayingSaved).By(Event.Replay).Then(Replay).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleSaved).To(State.SavingIdle).By(Event.RequestSaving).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleSaved).To(State.LoadingIdle).By(Event.RequestLoading).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleSaved).To(State.IdleUnsaved).By(Event.TrimStart).Then(TrimStart).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleSaved).To(State.IdleUnsaved).By(Event.TrimEnd).Then(TrimEnd).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleSaved).To(State.End).By(Event.Exit));
        Register(Transition.From(State.IdleSaved).To(State.IdleSaved).By(Event.Connect)); // NO-OP

        Register(Transition.From(State.IdleUnsaved).To(State.DisconnectedUnsaved).By(Event.Disconnect).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleUnsaved).To(State.Recording).By(Event.Record).Then(() =>
        {
            return dialogLogic.Confirm("You haven't saved the recording.\nStarting a new recording will remove current one.\nDo you want to proceed?");
        }).Then(StartRecording).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleUnsaved).To(State.ReplayingUnsaved).By(Event.Replay).Then(Replay).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleUnsaved).To(State.SavingIdle).By(Event.RequestSaving).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleUnsaved).To(State.LoadingIdle).By(Event.RequestLoading).Then(() =>
        {
            return dialogLogic.Confirm("You haven't saved the recording.\nLoading a recording will remove current one.\nDo you want to proceed?");
        }).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleUnsaved).To(State.IdleUnsaved).By(Event.TrimStart).Then(() =>
        {
            return dialogLogic.Confirm("You haven't saved the recording.\nTrimming will remove all frames before the current one.\nDo you want to proceed?");
        }).Then(TrimStart).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleUnsaved).To(State.IdleUnsaved).By(Event.TrimEnd).Then(() =>
        {
            return dialogLogic.Confirm("You haven't saved the recording.\nTrimming will remove all frames after the current one.\nDo you want to proceed?");
        }).Then(TrimEnd).ThenUpdate(viewModel));
        Register(Transition.From(State.IdleUnsaved).To(State.End).By(Event.Exit).Then(() =>
        {
            return dialogLogic.Confirm("You haven't saved the recording.\n\nDo you want to exit Flight Recorder without saving?");
        }));
        Register(Transition.From(State.IdleUnsaved).To(State.IdleUnsaved).By(Event.Connect)); // NO-OP

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

        Register(Transition.From(State.Recording).To(State.IdleUnsaved).By(Event.Stop).Then(StopRecording).ThenUpdate(viewModel));

        Register(Transition.From(State.ReplayingSaved).To(State.ReplayingSaved).By(Event.RequestStopping).Then(RequestStopping));
        Register(Transition.From(State.ReplayingSaved).To(State.PausingSaved).By(Event.Pause).Then(() => replayLogic.PauseReplay()).ThenUpdate(viewModel));
        Register(Transition.From(State.ReplayingSaved).To(State.IdleSaved).By(Event.Stop).Then(StopReplay).ThenUpdate(viewModel));

        Register(Transition.From(State.ReplayingUnsaved).To(State.ReplayingUnsaved).By(Event.RequestStopping).Then(RequestStopping));
        Register(Transition.From(State.ReplayingUnsaved).To(State.PausingUnsaved).By(Event.Pause).Then(() => replayLogic.PauseReplay()).ThenUpdate(viewModel));
        Register(Transition.From(State.ReplayingUnsaved).To(State.IdleUnsaved).By(Event.Stop).Then(StopReplay).ThenUpdate(viewModel));

        Register(Transition.From(State.PausingSaved).To(State.PausingSaved).By(Event.RequestStopping).Then(RequestStopping));
        Register(Transition.From(State.PausingSaved).To(State.ReplayingSaved).By(Event.Resume).Then(() => replayLogic.ResumeReplay()).ThenUpdate(viewModel));
        Register(Transition.From(State.PausingSaved).To(State.IdleSaved).By(Event.Stop).Then(StopReplay).ThenUpdate(viewModel));
        Register(Transition.From(State.PausingSaved).To(State.PausingUnsaved).By(Event.TrimStart).Then(TrimStart).ThenUpdate(viewModel));
        Register(Transition.From(State.PausingSaved).To(State.PausingUnsaved).By(Event.TrimEnd).Then(TrimEnd).ThenUpdate(viewModel));

        Register(Transition.From(State.PausingUnsaved).To(State.PausingUnsaved).By(Event.RequestStopping).Then(RequestStopping));
        Register(Transition.From(State.PausingUnsaved).To(State.ReplayingUnsaved).By(Event.Resume).Then(() => replayLogic.ResumeReplay()).ThenUpdate(viewModel));
        Register(Transition.From(State.PausingUnsaved).To(State.IdleUnsaved).By(Event.Stop).Then(StopReplay).ThenUpdate(viewModel));
        Register(Transition.From(State.PausingUnsaved).To(State.PausingUnsaved).By(Event.TrimStart).Then(() =>
        {
            return dialogLogic.Confirm("You haven't saved the recording.\nTrimming will remove all frames before the current one.\nDo you want to proceed?");
        }).Then(TrimStart).ThenUpdate(viewModel));
        Register(Transition.From(State.PausingUnsaved).To(State.PausingUnsaved).By(Event.TrimEnd).Then(() =>
        {
            return dialogLogic.Confirm("You haven't saved the recording.\nTrimming will remove all frames after the current one.\nDo you want to proceed?");
        }).Then(TrimEnd).ThenUpdate(viewModel));


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

    private bool StopRecording()
    {
        recorderLogic.StopRecording();
        replayLogic.FromData(null, recorderLogic.ToData(currentVersion));
        return true;
    }

    private async Task<bool> SaveRecordingAsync(ActionContext actionContext)
    {
        var data = replayLogic.ToData(currentVersion);

        async Task<string?> GetSavePath()
        {
            if (actionContext.FromShortcut)
            {
                var folder = await settingsLogic.GetDefaultSaveFolderAsync();
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    return Path.Combine(folder, $"{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.fltrec");
                }
            }
            return await dialogLogic.PickSaveFileAsync();
        }

        var filePath = await GetSavePath();

        if (string.IsNullOrEmpty(filePath))
        {
            return false;
        }

        await storageLogic.SaveAsync(filePath, data);
        replayLogic.FromData(Path.GetFileName(filePath), data);
        return true;
    }

    private async Task<bool> LoadRecordingAsync()
    {
        var selectedFile = await dialogLogic.PickOpenFileAsync();
        if (selectedFile != null)
        {
            var data = await storageLogic.LoadAsync(selectedFile.Value.fileStream);
            if (data != null)
            {
                replayLogic.FromData(Path.GetFileName(selectedFile.Value.filePath), data);
                return true;
            }
        }
        return false;
    }

    private bool RequestStopping()
    {
        replayLogic.StopReplay();
        return true;
    }

    private bool Replay()
    {
        return replayLogic.Replay();
    }

    private bool StopReplay()
    {
        viewModel.CurrentFrame = 0;
        return true;
    }

    private bool TrimStart()
    {
        replayLogic.TrimStart();
        return true;
    }

    private bool TrimEnd()
    {
        replayLogic.TrimEnd();
        return true;
    }
}
