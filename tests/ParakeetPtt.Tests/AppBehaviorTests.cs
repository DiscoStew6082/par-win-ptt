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
            Assert.AreEqual(StatusOverlayForm.DefaultSizeForTest, overlay.Size);
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
            Assert.AreEqual("Recording 00:00" + Environment.NewLine + "Release to transcribe", overlay.MessageTextForTest);
            Assert.IsTrue(overlay.TitleHeightForTest >= overlay.TitlePreferredHeightForTest + 10);
            Assert.IsTrue(overlay.MessageHeightForTest >= overlay.MessagePreferredHeightForTest + 10);
        });
    }

    [TestMethod]
    public void StatusOverlayReservesListeningTextAboveLargeActivityMeter()
    {
        RunOnStaThread(() =>
        {
            using var overlay = new StatusOverlayForm();

            overlay.ApplyStatusForTest(DictationStatusCatalog.Listening);

            Assert.AreEqual(560, StatusOverlayForm.ListeningSizeForTest.Width);
            Assert.AreEqual(StatusOverlayForm.ListeningSizeForTest, overlay.Size);
            Assert.AreEqual(288, overlay.ActivityMeterHeightForTest);
            Assert.IsTrue(overlay.TextPanelHeightForTest >= overlay.TitlePreferredHeightForTest + overlay.MessagePreferredHeightForTest + 20);
            Assert.IsTrue(overlay.ActivityMeterTopForTest >= overlay.TextPanelBottomForTest);
        });
    }

    [TestMethod]
    public void StatusOverlayReturnsToCompactSizeAfterListening()
    {
        RunOnStaThread(() =>
        {
            using var overlay = new StatusOverlayForm();
            overlay.ApplyStatusForTest(DictationStatusCatalog.Listening);

            overlay.ApplyStatusForTest(DictationStatusCatalog.Transcribing);

            Assert.AreEqual(StatusOverlayForm.DefaultSizeForTest, overlay.Size);
        });
    }

    [TestMethod]
    public void ListeningStatusFormatterShowsElapsedTimeAndReleaseHint()
    {
        var text = ListeningStatusFormatter.Format(TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(5));

        Assert.AreEqual("Recording 61:05" + Environment.NewLine + "Release to transcribe", text);
    }

    [TestMethod]
    public void ListeningStatusFormatterShowsToggleHintWithoutRelease()
    {
        var text = ListeningStatusFormatter.Format(
            TimeSpan.FromSeconds(9),
            ListeningTriggerMode.Toggle);

        Assert.AreEqual("Recording 00:09" + Environment.NewLine + "Press Right Shift to transcribe", text);
        StringAssert.DoesNotMatch(text, new System.Text.RegularExpressions.Regex("Release"));
    }

    [TestMethod]
    public void StatusOverlayShowsToggleListeningTextWithoutRelease()
    {
        RunOnStaThread(() =>
        {
            using var overlay = new StatusOverlayForm();

            overlay.ApplyStatusForTest(DictationStatusCatalog.Listening, ListeningTriggerMode.Toggle);

            Assert.AreEqual("Recording 00:00" + Environment.NewLine + "Press Right Shift to transcribe", overlay.MessageTextForTest);
        });
    }

    [TestMethod]
    public void AudioLevelCalculatorConvertsPcmSamplesToNormalizedPeak()
    {
        var pcm = new byte[6];
        WriteInt16(pcm, 0, 0);
        WriteInt16(pcm, 2, short.MaxValue / 2);
        WriteInt16(pcm, 4, short.MinValue);

        var level = AudioLevelCalculator.CalculatePeakLevel(pcm);

        Assert.AreEqual(1.0, level, 0.001);
    }

    [TestMethod]
    public void AudioLevelCalculatorHandlesSilenceAndOddByteCount()
    {
        var level = AudioLevelCalculator.CalculatePeakLevel([0, 0, 255]);

        Assert.AreEqual(0, level);
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
    public void StatusOverlayActivityMeterStoresLatestMicrophoneLevel()
    {
        RunOnStaThread(() =>
        {
            using var overlay = new StatusOverlayForm();
            overlay.ApplyStatusForTest(DictationStatusCatalog.Listening);

            overlay.UpdateActivityLevelForTest(0.75);

            Assert.AreEqual(0.75, overlay.LatestActivityLevelForTest, 0.001);
        });
    }

    [TestMethod]
    public void StatusOverlayActivityMeterUsesFixedVerticalResponseProfile()
    {
        RunOnStaThread(() =>
        {
            using var overlay = new StatusOverlayForm();
            overlay.ApplyStatusForTest(DictationStatusCatalog.Listening);

            overlay.UpdateActivityLevelForTest(0.75);
            var firstProfile = overlay.ActivityMeterBarHeightsForTest;

            overlay.UpdateActivityLevelForTest(0.75);
            var secondProfile = overlay.ActivityMeterBarHeightsForTest;

            var center = firstProfile.Length / 2;
            Assert.IsTrue(firstProfile[center] > firstProfile[0]);
            Assert.IsTrue(firstProfile[center] > firstProfile[^1]);
            CollectionAssert.AreEqual(firstProfile, secondProfile);
        });
    }

    [TestMethod]
    public void StatusOverlayDecaysMicrophoneLevelInsteadOfAnimatingFakeMotion()
    {
        RunOnStaThread(() =>
        {
            using var overlay = new StatusOverlayForm();
            overlay.ApplyStatusForTest(DictationStatusCatalog.Listening);
            overlay.UpdateActivityLevelForTest(0.8);

            overlay.AdvanceLiveActivityForTest();

            Assert.IsTrue(overlay.LatestActivityLevelForTest < 0.8);
            Assert.IsTrue(overlay.LatestActivityLevelForTest > 0);
        });
    }

    [TestMethod]
    public void StatusOverlayIgnoresMicrophoneLevelWhenNotListening()
    {
        RunOnStaThread(() =>
        {
            using var overlay = new StatusOverlayForm();

            overlay.ApplyStatusForTest(DictationStatusCatalog.Transcribing);
            overlay.UpdateActivityLevelForTest(0.75);

            Assert.AreEqual(0, overlay.LatestActivityLevelForTest);
        });
    }

    [TestMethod]
    public void StatusOverlayResetsActivityMeterForEachListeningSession()
    {
        RunOnStaThread(() =>
        {
            using var overlay = new StatusOverlayForm();
            overlay.ApplyStatusForTest(DictationStatusCatalog.Listening);
            overlay.UpdateActivityLevelForTest(0.75);

            overlay.ApplyStatusForTest(DictationStatusCatalog.Transcribing);
            overlay.ApplyStatusForTest(DictationStatusCatalog.Listening);

            Assert.AreEqual(0, overlay.LatestActivityLevelForTest);
            Assert.IsFalse(overlay.HasActivityHistoryForTest);
        });
    }

    [TestMethod]
    public void StatusOverlayPositionsAtBottomCenterOfWorkingArea()
    {
        var location = StatusOverlayForm.CalculateBottomCenterLocationForTest(
            new Rectangle(100, 50, 1200, 800),
            StatusOverlayForm.DefaultSizeForTest);

        Assert.AreEqual(new Point(420, 670), location);
    }

    [TestMethod]
    public void StatusOverlayListeningPositionAccountsForTallActivityMeter()
    {
        var location = StatusOverlayForm.CalculateBottomCenterLocationForTest(
            new Rectangle(100, 50, 1200, 800),
            StatusOverlayForm.ListeningSizeForTest);

        Assert.AreEqual(new Point(420, 410), location);
    }

    [TestMethod]
    public void StatusOverlayPositionStaysInsideNarrowWorkingArea()
    {
        var location = StatusOverlayForm.CalculateBottomCenterLocationForTest(
            new Rectangle(100, 50, 420, 800),
            StatusOverlayForm.DefaultSizeForTest);

        Assert.AreEqual(new Point(120, 670), location);
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

    private static void WriteInt16(byte[] buffer, int offset, short sample)
    {
        var bytes = BitConverter.GetBytes(sample);
        buffer[offset] = bytes[0];
        buffer[offset + 1] = bytes[1];
    }
}
