using System;

namespace FlightRecorder.Client.SimConnectMSFS;

public interface IConnector
{
    bool IsInitialized { get; }

    event EventHandler<SimStateUpdatedEventArgs> SimStateUpdated;
    event EventHandler<AircraftPositionUpdatedEventArgs> AircraftPositionUpdated;
    event EventHandler Closed;
    event EventHandler<AircraftIdReceivedEventArgs> AircraftIdReceived;
    event EventHandler Frame;
    event EventHandler Initialized;
    event EventHandler CreatingObjectFailed;

    void Initialize(IntPtr Handle);
    bool HandleWindowsEvent(int message);
    void Init(uint aircraftId, AircraftPositionStruct position);
    void Freeze(uint aircraftId);
    void Unfreeze(uint aircraftId);
    /// <returns>Request ID</returns>
    uint Spawn(string aircraftTitle, AircraftPositionStruct position);
    void Despawn(uint aircraftId);
    void Set(uint aircraftId, AircraftPositionSetStruct position);
    void TriggerEvents(AircraftPositionStruct current, AircraftPositionStruct expected);
}