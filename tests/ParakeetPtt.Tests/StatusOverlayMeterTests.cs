using ParakeetPtt.App;
using ParakeetPtt.Core;

namespace ParakeetPtt.Tests;

[TestClass]
public sealed class StatusOverlayMeterTests
{
    [TestMethod]
    public void StatusOverlayActivityMeterEmphasizesCenterBarsAtModerateLevels()
    {
        RunOnStaThread(() =>
        {
            using var overlay = new StatusOverlayForm();
            overlay.ApplyStatusForTest(DictationStatusCatalog.Listening);

            overlay.UpdateActivityLevelForTest(0.35);

            var profile = overlay.ActivityMeterBarHeightsForTest;
            var center = profile.Length / 2;
            var tallestEdge = Math.Max(profile[0], profile[^1]);

            Assert.IsTrue(profile[center] >= 24);
            Assert.IsTrue(profile[center] >= tallestEdge * 2);
        });
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
