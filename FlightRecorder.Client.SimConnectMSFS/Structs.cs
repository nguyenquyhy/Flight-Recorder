using System.Runtime.InteropServices;

namespace FlightRecorder.Client
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct AircraftPositionStruct
    {
        //public int SimRate;

        public double Latitude;
        public double Longitude;
        public double Altitude;

        public double Pitch;
        public double Bank;
        public double TrueHeading;
        public double MagneticHeading;

        public double VelocityBodyX;
        public double VelocityBodyY;
        public double VelocityBodyZ;
        public double RotationVelocityBodyX;
        public double RotationVelocityBodyY;
        public double RotationVelocityBodyZ;

        public double AileronPosition;
        public double ElevatorPosition;
        public double RudderPosition;

        public double ElevatorTrimPosition;

        public double TrailingEdgeFlapsLeftPercent;
        public double TrailingEdgeFlapsRightPercent;
        public double LeadingEdgeFlapsLeftPercent;
        public double LeadingEdgeFlapsRightPercent;

        public double ThrottleLeverPosition1;
        public double ThrottleLeverPosition2;
        public double ThrottleLeverPosition3;
        public double ThrottleLeverPosition4;

        public double BrakeLeftPosition;
        public double BrakeRightPosition;

        public static AircraftPositionStruct operator *(AircraftPositionStruct position, double factor)
            => AircraftPositionStructOperator.Scale(position, factor);

        public static AircraftPositionStruct operator +(AircraftPositionStruct position1, AircraftPositionStruct position2)
            => AircraftPositionStructOperator.Add(position1, position2);
    }

    /// <summary>
    /// This class is auto-generated to perform calculation on all fields.
    /// </summary>
    public partial class AircraftPositionStructOperator
    {
        public static partial AircraftPositionStruct Add(AircraftPositionStruct position1, AircraftPositionStruct position2);
        public static partial AircraftPositionStruct Scale(AircraftPositionStruct position, double factor);
    }

    /// <summary>
    /// This class is auto-generated to converter fields in the struct into properties.
    /// </summary>
    public partial class AircraftPosition
    {
        public static partial AircraftPosition FromStruct(AircraftPositionStruct s);
    }
}
