namespace FlightRecorder.Client.SimConnectMSFS
{
    enum EVENTS
    {
        GENERIC,
        PAUSE,
        UNPAUSE,
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
