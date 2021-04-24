namespace FlightRecorder.Client.SimConnectMSFS
{
    enum EVENTS
    {
        GENERIC,
        FREEZE_LATITUDE_LONGITUDE,
        FREEZE_ALTITUDE,
        FREEZE_ATTITUDE,
        FRAME,
    }
    enum GROUPS
    {
        GENERIC = 0
    }

    enum DEFINITIONS
    {
        SimState,
        AircraftPositionInitial,
        AircraftPosition,
        AircraftPositionSet,
    }

    internal enum DATA_REQUESTS
    {
        SIM_STATE,
        AIRCRAFT_POSITION,
    }
}
