namespace ParakeetPtt.Core;

public sealed class DictationController(
    IAudioRecorder recorder,
    ITranscriber transcriber,
    IClipboardPaster clipboardPaster,
    SessionHistory history)
{
    private bool _isRecording;
    private bool _isProcessing;

    public async Task<bool> HandleHotkeyDownAsync(CancellationToken cancellationToken)
    {
        if (_isRecording || _isProcessing)
        {
            return false;
        }

        _isRecording = true;
        await recorder.StartAsync(cancellationToken);
        return true;
    }

    public async Task<DictationOutcome> HandleHotkeyUpAsync(CancellationToken cancellationToken)
    {
        if (!_isRecording || _isProcessing)
        {
            return DictationOutcome.NotRecording;
        }

        _isRecording = false;
        _isProcessing = true;

        RecordedAudio? audio = null;
        try
        {
            audio = await recorder.StopAsync(cancellationToken);
            var result = await transcriber.TranscribeAsync(audio.Path, cancellationToken);
            var cleaned = TranscriptNormalizer.Normalize(result.Text);
            if (cleaned.Length == 0)
            {
                return DictationOutcome.EmptyTranscript;
            }

            history.Add(cleaned);
            await clipboardPaster.PasteAsync(cleaned, cancellationToken);
            return DictationOutcome.Pasted;
        }
        finally
        {
            if (audio is { DeleteAfterUse: true })
            {
                TryDelete(audio.Path);
            }

            _isProcessing = false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public enum DictationOutcome
{
    NotRecording,
    EmptyTranscript,
    Pasted
}
