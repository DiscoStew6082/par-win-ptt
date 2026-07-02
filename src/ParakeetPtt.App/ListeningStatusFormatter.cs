namespace ParakeetPtt.App;

internal static class ListeningStatusFormatter
{
    public static string Format(TimeSpan elapsed)
    {
        var clamped = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        return $"Recording {(int)clamped.TotalMinutes:00}:{clamped.Seconds:00}{Environment.NewLine}Release to transcribe";
    }
}
