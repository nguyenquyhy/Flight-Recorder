namespace FlightRecorder.Client.SimConnectMSFS
{
    enum EVENTS
    {
        GENERIC,
        PAUSE,
        UNPAUSE,
        LEFT_BRAKE_SET,
        RIGHT_BRAKE_SET,
    }
    enum GROUPS
    {
        GENERIC = 0
    }

    enum DEFINITIONS
    {
        AircraftPosition,
    }

    internal enum DATA_REQUESTS
    {
        AIRCRAFT_POSITION,
    }
}
