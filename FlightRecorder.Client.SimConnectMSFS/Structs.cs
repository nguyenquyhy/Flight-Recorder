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
            => new AircraftPositionStruct
            {
                Latitude = position.Latitude * factor,
                Longitude = position.Longitude * factor,
                Altitude = position.Altitude * factor,
                Pitch = position.Pitch * factor,
                Bank = position.Bank * factor,
                TrueHeading = position.TrueHeading * factor,
                MagneticHeading = position.MagneticHeading * factor,
                VelocityBodyX = position.VelocityBodyX * factor, // TODO: wrap around
                VelocityBodyY = position.VelocityBodyY * factor,
                VelocityBodyZ = position.VelocityBodyZ * factor,
                RotationVelocityBodyX = position.RotationVelocityBodyX * factor,
                RotationVelocityBodyY = position.RotationVelocityBodyY * factor,
                RotationVelocityBodyZ = position.RotationVelocityBodyZ * factor,
                AileronPosition = position.AileronPosition * factor,
                ElevatorPosition = position.ElevatorPosition * factor,
                RudderPosition = position.RudderPosition * factor,

                ElevatorTrimPosition = position.ElevatorTrimPosition * factor,

                TrailingEdgeFlapsLeftPercent = position.TrailingEdgeFlapsLeftPercent * factor,
                TrailingEdgeFlapsRightPercent = position.TrailingEdgeFlapsRightPercent * factor,
                LeadingEdgeFlapsLeftPercent = position.LeadingEdgeFlapsLeftPercent * factor,
                LeadingEdgeFlapsRightPercent = position.LeadingEdgeFlapsRightPercent * factor,

                ThrottleLeverPosition1 = position.ThrottleLeverPosition1 * factor,
                ThrottleLeverPosition2 = position.ThrottleLeverPosition2 * factor,
                ThrottleLeverPosition3 = position.ThrottleLeverPosition3 * factor,
                ThrottleLeverPosition4 = position.ThrottleLeverPosition4 * factor,

                BrakeLeftPosition = position.BrakeLeftPosition * factor,
                BrakeRightPosition = position.BrakeRightPosition * factor,
            };

        public static AircraftPositionStruct operator +(AircraftPositionStruct position1, AircraftPositionStruct position2)
            => new AircraftPositionStruct
            {
                Latitude = position1.Latitude + position2.Latitude,
                Longitude = position1.Longitude + position2.Longitude,
                Altitude = position1.Altitude + position2.Altitude,
                Pitch = position1.Pitch + position2.Pitch,
                Bank = position1.Bank + position2.Bank,
                TrueHeading = position1.TrueHeading + position2.TrueHeading,
                MagneticHeading = position1.MagneticHeading + position2.MagneticHeading,
                VelocityBodyX = position1.VelocityBodyX + position2.VelocityBodyX,
                VelocityBodyY = position1.VelocityBodyY + position2.VelocityBodyY,
                VelocityBodyZ = position1.VelocityBodyZ + position2.VelocityBodyZ,
                RotationVelocityBodyX = position1.RotationVelocityBodyX + position2.RotationVelocityBodyX,
                RotationVelocityBodyY = position1.RotationVelocityBodyY + position2.RotationVelocityBodyY,
                RotationVelocityBodyZ = position1.RotationVelocityBodyZ + position2.RotationVelocityBodyZ,
                AileronPosition = position1.AileronPosition + position2.AileronPosition,
                ElevatorPosition = position1.ElevatorPosition + position2.ElevatorPosition,
                RudderPosition = position1.RudderPosition + position2.RudderPosition,

                ElevatorTrimPosition = position1.ElevatorTrimPosition + position2.ElevatorTrimPosition,

                TrailingEdgeFlapsLeftPercent = position1.TrailingEdgeFlapsLeftPercent + position2.TrailingEdgeFlapsLeftPercent,
                TrailingEdgeFlapsRightPercent = position1.TrailingEdgeFlapsRightPercent + position2.TrailingEdgeFlapsRightPercent,
                LeadingEdgeFlapsLeftPercent = position1.LeadingEdgeFlapsLeftPercent + position2.LeadingEdgeFlapsLeftPercent,
                LeadingEdgeFlapsRightPercent = position1.LeadingEdgeFlapsRightPercent + position2.LeadingEdgeFlapsRightPercent,

                ThrottleLeverPosition1 = position1.ThrottleLeverPosition1 + position2.ThrottleLeverPosition1,
                ThrottleLeverPosition2 = position1.ThrottleLeverPosition2 + position2.ThrottleLeverPosition2,
                ThrottleLeverPosition3 = position1.ThrottleLeverPosition3 + position2.ThrottleLeverPosition3,
                ThrottleLeverPosition4 = position1.ThrottleLeverPosition4 + position2.ThrottleLeverPosition4,

                BrakeLeftPosition = position1.BrakeLeftPosition + position2.BrakeLeftPosition,
                BrakeRightPosition = position1.BrakeRightPosition + position2.BrakeRightPosition,
            };
    }

    /// <summary>
    /// This class is auto-generated to converter fields in the struct into properties.
    /// </summary>
    public partial class AircraftPosition
    {
        public static partial AircraftPosition FromStruct(AircraftPositionStruct s);
    }
}
