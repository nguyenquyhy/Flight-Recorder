using FlightRecorder.Client.Logics;
using FlightRecorder.Client.SimConnectMSFS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FlightRecorder.Client.ViewModels.Tests
{
    [TestClass]
    public class TestStateMachine
    {
        private Mock<IThreadLogic> mockThreadLogic;
        private Mock<IRecorderLogic> mockRecorderLogic;
        private Mock<IReplayLogic> mockReplayLogic;
        private Mock<IConnector> mockConnector;
        private Mock<IDialogLogic> mockDialogLogic;

        private MainViewModel viewModel;
        private StateMachine stateMachine;

        [TestInitialize]
        public void Setup()
        {
            var serviceProvider = new ServiceCollection()
                .AddLogging(builder => builder.AddDebug())
                .BuildServiceProvider();

            var factory = serviceProvider.GetService<ILoggerFactory>();

            mockThreadLogic = new Mock<IThreadLogic>();
            mockRecorderLogic = new Mock<IRecorderLogic>();
            mockReplayLogic = new Mock<IReplayLogic>();
            mockConnector = new Mock<IConnector>();
            mockDialogLogic = new Mock<IDialogLogic>();

            viewModel = new MainViewModel(
                factory.CreateLogger<MainViewModel>(),
                mockThreadLogic.Object,
                mockRecorderLogic.Object,
                mockReplayLogic.Object,
                mockConnector.Object);

            stateMachine = new StateMachine(
                factory.CreateLogger<StateMachine>(),
                viewModel,
                mockRecorderLogic.Object,
                mockReplayLogic.Object,
                mockDialogLogic.Object);
        }

        [TestMethod]
        public async Task TestAllEvents()
        {
            Assert.AreEqual(StateMachine.State.Start, viewModel.State);

            await stateMachine.TransitAsync(StateMachine.Event.StartUp);

            Assert.AreEqual(StateMachine.State.DisconnectedEmpty, viewModel.State);

            mockDialogLogic.Setup(logic => logic.LoadAsync())
                .Returns(Task.FromResult(("test", new SavedData(
                    "TEST_VERSION",
                    0,
                    1,
                    null,
                    new List<(long milliseconds, AircraftPositionStruct position)>()
                ))));
            await stateMachine.TransitAsync(StateMachine.Event.Load);
            Assert.AreEqual(StateMachine.State.DisconnectedSaved, viewModel.State);
        }

        [TestMethod]
        public async Task TestMultipleTransitions()
        {
            mockReplayLogic.Setup(logic => logic.Replay()).Returns(true);

            await stateMachine.TransitAsync(StateMachine.Event.StartUp);
            Assert.AreEqual(StateMachine.State.DisconnectedEmpty, viewModel.State);
            await stateMachine.TransitAsync(StateMachine.Event.Connect);
            Assert.AreEqual(StateMachine.State.IdleEmpty, viewModel.State);
            await stateMachine.TransitAsync(StateMachine.Event.Record);
            Assert.AreEqual(StateMachine.State.Recording, viewModel.State);
            await stateMachine.TransitAsync(StateMachine.Event.Stop);
            Assert.AreEqual(StateMachine.State.IdleUnsaved, viewModel.State);

            await stateMachine.TransitAsync(StateMachine.Event.Replay);
            Assert.AreEqual(StateMachine.State.ReplayingUnsaved, viewModel.State);

            mockReplayLogic.Setup(logic => logic.StopReplay()).Callback(() =>
            {
                Task.Run(async () =>
                {
                    await stateMachine.TransitAsync(StateMachine.Event.Stop);
                });
            });
            Task.WaitAll(new Task[] {
                stateMachine.TransitAsync(StateMachine.Event.Disconnect),
            }, TimeSpan.FromSeconds(5));
            Assert.AreEqual(StateMachine.State.DisconnectedUnsaved, viewModel.State);
        }
    }
}
