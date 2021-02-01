namespace FlightRecorder.Client.SimConnectMSFS
{
    enum EVENTS
    {
        GENERIC,
        PAUSE,
        UNPAUSE,
        PARKING_BRAKES,
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
