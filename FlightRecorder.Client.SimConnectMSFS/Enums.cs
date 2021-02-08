namespace FlightRecorder.Client.SimConnectMSFS
{
    enum EVENTS
    {
        GENERIC,
        FREEZE_LATITUDE_LONGITUDE,
        FREEZE_ALTITUDE,
        FREEZE_ATTITUDE,
    }
    enum GROUPS
    {
        GENERIC = 0
    }

    enum DEFINITIONS
    {
        AircraftPosition,
        AircraftPositionSet,
    }

    internal enum DATA_REQUESTS
    {
        AIRCRAFT_POSITION,
    }
}
