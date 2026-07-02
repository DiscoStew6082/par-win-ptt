namespace ParakeetPtt.Core;

public interface IAudioRecorder
{
    Task StartAsync(CancellationToken cancellationToken);
    Task<RecordedAudio> StopAsync(CancellationToken cancellationToken);
}

public interface ITranscriber
{
    Task<TranscriptResult> TranscribeAsync(string wavPath, CancellationToken cancellationToken);
}

public interface IClipboardPaster
{
    Task PasteAsync(string text, CancellationToken cancellationToken);
}

public sealed record RecordedAudio(string Path, TimeSpan Duration, bool DeleteAfterUse = false);

public sealed record TranscriptResult(string Text, TimeSpan? InferenceTime, double? Confidence);
