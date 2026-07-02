using ParakeetPtt.App;
using ParakeetPtt.Core;

namespace ParakeetPtt.Tests;

[TestClass]
public sealed class AppBehaviorTests
{
    [TestMethod]
    public void StatusOverlayUsesNonActivatingTopmostWindow()
    {
        RunOnStaThread(() =>
        {
            using var overlay = new StatusOverlayForm();

            Assert.IsTrue(overlay.ShowWithoutActivationForTest);
            Assert.IsTrue((overlay.ExtendedWindowStyleForTest & StatusOverlayForm.NoActivateExtendedStyleForTest) != 0);
            Assert.IsTrue(overlay.TopMost);
            Assert.IsFalse(overlay.ShowInTaskbar);
            Assert.AreEqual(FormStartPosition.Manual, overlay.StartPosition);
        });
    }

    [TestMethod]
    public void StatusOverlayAutoHidesCompletionStatesWithoutShowingWindow()
    {
        RunOnStaThread(() =>
        {
            using var overlay = new StatusOverlayForm();

            overlay.ApplyStatusForTest(DictationStatusCatalog.Pasted);

            Assert.AreEqual("Pasted", overlay.TitleTextForTest);
            Assert.AreEqual("Transcript pasted into the active app.", overlay.MessageTextForTest);
            Assert.IsTrue(overlay.AutoHideTimerEnabledForTest);
            Assert.IsFalse(overlay.Visible);
        });
    }

    [TestMethod]
    public void StatusOverlayKeepsListeningTextAwayFromClippedEdges()
    {
        RunOnStaThread(() =>
        {
            using var overlay = new StatusOverlayForm();

            overlay.ApplyStatusForTest(DictationStatusCatalog.Listening);

            Assert.AreEqual(ContentAlignment.MiddleLeft, overlay.TitleAlignmentForTest);
            Assert.AreEqual(ContentAlignment.MiddleLeft, overlay.MessageAlignmentForTest);
            Assert.IsTrue(overlay.TitleHeightForTest >= overlay.TitlePreferredHeightForTest + 10);
            Assert.IsTrue(overlay.MessageHeightForTest >= overlay.MessagePreferredHeightForTest + 10);
        });
    }

    [TestMethod]
    public void ListeningStatusFormatterShowsElapsedTimeAndReleaseHint()
    {
        var text = ListeningStatusFormatter.Format(TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(5));

        Assert.AreEqual("Recording 61:05 - release Right Ctrl to transcribe.", text);
    }

    [TestMethod]
    public void StatusOverlayRunsLiveActivityOnlyWhileListening()
    {
        RunOnStaThread(() =>
        {
            using var overlay = new StatusOverlayForm();

            overlay.ApplyStatusForTest(DictationStatusCatalog.Listening);

            Assert.IsTrue(overlay.LiveActivityTimerEnabledForTest);
            Assert.IsTrue(overlay.ActivityMeterVisibleForTest);
            StringAssert.Contains(overlay.MessageTextForTest, "Recording 00:00");

            overlay.ApplyStatusForTest(DictationStatusCatalog.Transcribing);

            Assert.IsFalse(overlay.LiveActivityTimerEnabledForTest);
            Assert.IsFalse(overlay.ActivityMeterVisibleForTest);
        });
    }

    [TestMethod]
    public void StatusOverlayPositionsAtBottomCenterOfWorkingArea()
    {
        var location = StatusOverlayForm.CalculateBottomCenterLocationForTest(
            new Rectangle(100, 50, 1200, 800),
            StatusOverlayForm.DefaultSizeForTest);

        Assert.AreEqual(new Point(420, 686), location);
    }

    [TestMethod]
    public void StatusOverlayPositionStaysInsideNarrowWorkingArea()
    {
        var location = StatusOverlayForm.CalculateBottomCenterLocationForTest(
            new Rectangle(100, 50, 420, 800),
            StatusOverlayForm.DefaultSizeForTest);

        Assert.AreEqual(new Point(120, 686), location);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            throw exception;
        }
    }
}
