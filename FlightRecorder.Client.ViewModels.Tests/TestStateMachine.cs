using FlightRecorder.Client.Logics;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;

namespace FlightRecorder.Client.ViewModels.Tests
{
    [TestClass]
    public class TestStateMachine
    {
        [TestMethod]
        public async Task TestAllEvents()
        {
            var viewModel = new MainViewModel();

            Assert.AreEqual(StateMachine.State.Start, viewModel.State);

            var stateMachine = new StateMachine(new Mock<ILogger<StateMachine>>().Object, viewModel, new Mock<IRecorderLogic>().Object, new Mock<IDialogLogic>().Object);
            await stateMachine.TransitAsync(StateMachine.Event.StartUp);

            Assert.AreEqual(StateMachine.State.DisconnectedSaved, viewModel.State);
        }
    }
}
