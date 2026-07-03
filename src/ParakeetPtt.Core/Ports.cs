namespace ParakeetPtt.Core;

public interface IAudioRecorder
{
    Task StartAsync(CancellationToken cancellationToken);
    Task<RecordedAudio> StopAsync(CancellationToken cancellationToken);
}

public interface IChunkedAudioRecorder : IAudioRecorder
{
    event Action<RecordedAudio>? AudioChunkReady;
}

public interface ITranscriber
{
    Task<TranscriptResult> TranscribeAsync(string wavPath, CancellationToken cancellationToken);
}

public interface IDictationSessionFactory
{
    IDictationSession CreateSession();
}

public interface IDictationSession
{
    event Action<TranscriptUpdate>? TranscriptUpdated;

    Task StartAsync(CancellationToken cancellationToken);

    Task<DictationSessionResult> StopAsync(CancellationToken cancellationToken);
}

public interface IClipboardPaster
{
    Task PasteAsync(string text, CancellationToken cancellationToken);
}

public sealed record RecordedAudio(string Path, TimeSpan Duration, bool DeleteAfterUse = false);

public sealed record TranscriptResult(string Text, TimeSpan? InferenceTime, double? Confidence);

public sealed record DictationSessionResult(TranscriptResult Transcript, RecordedAudio? FinalAudio = null);

public sealed record TranscriptUpdate(TranscriptUpdateKind Kind, string StableText, string UnstableText = "");

public enum TranscriptUpdateKind
{
    Partial,
    Final
}
