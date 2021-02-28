using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace FlightRecorder.Client.Logics.Tests
{
    [TestClass]
    public class InterpolationTests
    {
        private const double Epsilon = 0.000001;

        [TestMethod]
        public void TestWrapToFront()
        {
            var result = AircraftPositionStructOperator.InterpolateWrap(0.1, 0.9, 0.5, 0, 1);
            Assert.IsTrue(result < Epsilon);
        }

        [TestMethod]
        public void TestWrapToBack()
        {
            var result = AircraftPositionStructOperator.InterpolateWrap(0.1, 0.8, 0.5, 0, 1);
            Assert.IsTrue(Math.Abs(result - 0.95)  < Epsilon);
        }

        [TestMethod]
        public void TestNoWrap()
        {
            var result = AircraftPositionStructOperator.InterpolateWrap(0.1, 0.6, 0.5, 0, 1);
            Assert.IsTrue(Math.Abs(result - 0.35) < Epsilon);
        }
    }
}
