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
        AircraftPositionInitial,
        AircraftPosition,
        AircraftPositionSet,
    }

    internal enum DATA_REQUESTS
    {
        AIRCRAFT_POSITION,
    }
}
