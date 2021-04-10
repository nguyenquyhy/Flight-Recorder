using FlightRecorder.Client.Logics;
using FlightRecorder.Client.SimConnectMSFS;
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
            var mockThreadLogic = new Mock<IThreadLogic>().Object;
            var mockRecorderLogic = new Mock<IRecorderLogic>().Object;
            var mockReplayLogic = new Mock<IReplayLogic>().Object;
            var mockConnector = new Mock<IConnector>().Object;

            var viewModel = new MainViewModel(
                new Mock<ILogger<MainViewModel>>().Object,
                mockThreadLogic,
                mockRecorderLogic,
                mockReplayLogic,
                mockConnector);

            Assert.AreEqual(StateMachine.State.Start, viewModel.State);

            var stateMachine = new StateMachine(
                new Mock<ILogger<StateMachine>>().Object,
                viewModel,
                mockRecorderLogic,
                mockReplayLogic,
                new Mock<IDialogLogic>().Object);
            await stateMachine.TransitAsync(StateMachine.Event.StartUp);

            Assert.AreEqual(StateMachine.State.DisconnectedEmpty, viewModel.State);
        }
    }
}
