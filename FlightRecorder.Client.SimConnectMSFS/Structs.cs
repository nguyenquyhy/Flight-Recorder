using Microsoft.FlightSimulator.SimConnect;
using System.Runtime.InteropServices;

namespace FlightRecorder.Client
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct AircraftPositionStruct
    {
        //public int SimRate;

        [SimConnectVariable(Name = "PLANE LATITUDE", Unit = "Degrees", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double Latitude;
        [SimConnectVariable(Name = "PLANE LONGITUDE", Unit = "Degrees", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double Longitude;
        [SimConnectVariable(Name = "PLANE ALTITUDE", Unit = "Feet", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double Altitude;

        [SimConnectVariable(Name = "PLANE PITCH DEGREES", Unit = "Degrees", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double Pitch;
        [SimConnectVariable(Name = "PLANE BANK DEGREES", Unit = "Degrees", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double Bank;
        [SimConnectVariable(Name = "PLANE HEADING DEGREES TRUE", Unit = "Degrees", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double TrueHeading;
        [SimConnectVariable(Name = "PLANE HEADING DEGREES MAGNETIC", Unit = "Degrees", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double MagneticHeading;

        [SimConnectVariable(Name = "VELOCITY BODY X", Unit = "Feet per second", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double VelocityBodyX;
        [SimConnectVariable(Name = "VELOCITY BODY Y", Unit = "Feet per second", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double VelocityBodyY;
        [SimConnectVariable(Name = "VELOCITY BODY Z", Unit = "Feet per second", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double VelocityBodyZ;
        [SimConnectVariable(Name = "ROTATION VELOCITY BODY X", Unit = "radians per second", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double RotationVelocityBodyX;
        [SimConnectVariable(Name = "ROTATION VELOCITY BODY Y", Unit = "radians per second", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double RotationVelocityBodyY;
        [SimConnectVariable(Name = "ROTATION VELOCITY BODY Z", Unit = "radians per second", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double RotationVelocityBodyZ;

        [SimConnectVariable(Name = "AILERON POSITION", Unit = "Position", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double AileronPosition;
        [SimConnectVariable(Name = "ELEVATOR POSITION", Unit = "Position", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double ElevatorPosition;
        [SimConnectVariable(Name = "RUDDER POSITION", Unit = "Position", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double RudderPosition;

        [SimConnectVariable(Name = "ELEVATOR TRIM POSITION", Unit = "Radians", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double ElevatorTrimPosition;

        [SimConnectVariable(Name = "TRAILING EDGE FLAPS LEFT PERCENT", Unit = "Position", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double TrailingEdgeFlapsLeftPercent;
        [SimConnectVariable(Name = "TRAILING EDGE FLAPS RIGHT PERCENT", Unit = "Position", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double TrailingEdgeFlapsRightPercent;
        [SimConnectVariable(Name = "LEADING EDGE FLAPS LEFT PERCENT", Unit = "Position", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double LeadingEdgeFlapsLeftPercent;
        [SimConnectVariable(Name = "LEADING EDGE FLAPS RIGHT PERCENT", Unit = "Position", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double LeadingEdgeFlapsRightPercent;

        [SimConnectVariable(Name = "GENERAL ENG THROTTLE LEVER POSITION:1", Unit = "Position", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double ThrottleLeverPosition1;
        [SimConnectVariable(Name = "GENERAL ENG THROTTLE LEVER POSITION:2", Unit = "Position", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double ThrottleLeverPosition2;
        [SimConnectVariable(Name = "GENERAL ENG THROTTLE LEVER POSITION:3", Unit = "Position", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double ThrottleLeverPosition3;
        [SimConnectVariable(Name = "GENERAL ENG THROTTLE LEVER POSITION:4", Unit = "Position", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double ThrottleLeverPosition4;

        [SimConnectVariable(Name = "SPOILERS HANDLE POSITION", Unit = "Percent", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double SpoilerHandlePosition;

        [SimConnectVariable(Name = "BRAKE LEFT POSITION", Unit = "Position", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double BrakeLeftPosition;
        [SimConnectVariable(Name = "BRAKE RIGHT POSITION", Unit = "Position", Type = SIMCONNECT_DATATYPE.FLOAT64)]
        public double BrakeRightPosition;

        [SimConnectVariable(Name = "BRAKE PARKING POSITION", Unit = "Position", Type = SIMCONNECT_DATATYPE.INT32, SetByEvent = "PARKING_BRAKES")]
        public uint BrakeParkingPosition;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public partial struct AircraftPositionSetStruct
    {
        public static AircraftPositionSetStruct operator *(AircraftPositionSetStruct position, double factor)
            => AircraftPositionStructOperator.Scale(position, factor);

        public static AircraftPositionSetStruct operator +(AircraftPositionSetStruct position1, AircraftPositionSetStruct position2)
            => AircraftPositionStructOperator.Add(position1, position2);
    }

    /// <summary>
    /// This class is auto-generated to perform calculation on all fields.
    /// </summary>
    public partial class AircraftPositionStructOperator
    {
        public static partial AircraftPositionSetStruct ToSet(AircraftPositionStruct variables);
        public static partial AircraftPositionSetStruct Add(AircraftPositionSetStruct position1, AircraftPositionSetStruct position2);
        public static partial AircraftPositionSetStruct Scale(AircraftPositionSetStruct position, double factor);
    }

    /// <summary>
    /// This class is auto-generated to converter fields in the struct into properties.
    /// </summary>
    public partial class AircraftPosition
    {
        public static partial AircraftPosition FromStruct(AircraftPositionStruct s);
    }
}
