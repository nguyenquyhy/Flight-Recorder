using FlightRecorder.Client.Logics;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace FlightRecorder.Client.ViewModels.Tests;

[TestClass]
public class ShortcutKeyLogicTests
{
    private ShortcutKeyLogic shortcutKeyLogic = null!;

    [TestInitialize]
    public void Setup()
    {
        shortcutKeyLogic = new ShortcutKeyLogic(
            new Mock<ILogger<ShortcutKeyLogic>>().Object,
            new Mock<IStateMachine>().Object,
            new Mock<IThreadLogic>().Object,
            new Mock<ISettingsLogic>().Object
        );
    }

    [TestMethod]
    public void TestIsDuplicateEmpty()
    {
        var result = shortcutKeyLogic.IsDuplicate(
            Shortcuts.Record, 
            new(true, true, true, "C", 'C'), 
            []);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TestIsDuplicateSameShortcuts()
    {
        var result = shortcutKeyLogic.IsDuplicate(
            Shortcuts.Record, 
            new(true, true, true, "C", 'C'), 
            new()
            {
                [Shortcuts.Record] = new(true, true, true, "C", 'C'),
            });
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TestIsDuplicateDifferentShortcutsSameKeys()
    {
        var result = shortcutKeyLogic.IsDuplicate(
            Shortcuts.Record,
            new(true, true, true, "C", 'C'),
            new()
            {
                [Shortcuts.StopReplay] = new(true, true, true, "C", 'C'),
            });
        Assert.IsTrue(result);
    }
}
