using FlightRecorder.Client.Logics;
using System.Threading.Tasks;

namespace FlightRecorder.Client;

/// <summary>
/// Handle emergency save on crash
/// </summary>
public interface ICrashLogic
{
    Task LoadDataAsync(StateMachine stateMachine, IReplayLogic replayLogic);
    void SaveData();
}
