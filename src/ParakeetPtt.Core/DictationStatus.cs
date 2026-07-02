namespace ParakeetPtt.Core;

public sealed record DictationStatus(
    DictationStatusKind Kind,
    string Title,
    string Message,
    bool AutoHide);

public enum DictationStatusKind
{
    Listening,
    Transcribing,
    TranscriptPreview,
    Pasted,
    EmptyTranscript,
    Error
}

public static class DictationStatusCatalog
{
    public static DictationStatus Listening { get; } = new(
        DictationStatusKind.Listening,
        "Listening",
        "Release Right Ctrl to transcribe.",
        AutoHide: false);

    public static DictationStatus Transcribing { get; } = new(
        DictationStatusKind.Transcribing,
        "Transcribing",
        "Sending audio to local parakeet-cli.",
        AutoHide: false);

    public static DictationStatus Pasted { get; } = new(
        DictationStatusKind.Pasted,
        "Pasted",
        "Transcript pasted into the active app.",
        AutoHide: true);

    public static DictationStatus PastedTranscript(string text)
    {
        return new DictationStatus(
            DictationStatusKind.Pasted,
            "Pasted",
            text,
            AutoHide: true);
    }

    public static DictationStatus TranscriptPreview(string text)
    {
        return new DictationStatus(
            DictationStatusKind.TranscriptPreview,
            "Transcript",
            text,
            AutoHide: false);
    }

    public static DictationStatus EmptyTranscript { get; } = new(
        DictationStatusKind.EmptyTranscript,
        "No speech detected",
        "Nothing was pasted.",
        AutoHide: true);

    public static DictationStatus Error(string message)
    {
        return new DictationStatus(
            DictationStatusKind.Error,
            "Dictation failed",
            message,
            AutoHide: true);
    }
}
