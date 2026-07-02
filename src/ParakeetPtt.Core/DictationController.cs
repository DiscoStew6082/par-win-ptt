namespace ParakeetPtt.Core;

public sealed class DictationController(
    IAudioRecorder recorder,
    ITranscriber transcriber,
    IClipboardPaster clipboardPaster,
    SessionHistory history)
{
    private bool _isRecording;
    private bool _isProcessing;

    public async Task HandleHotkeyDownAsync(CancellationToken cancellationToken)
    {
        if (_isRecording || _isProcessing)
        {
            return;
        }

        _isRecording = true;
        await recorder.StartAsync(cancellationToken);
    }

    public async Task HandleHotkeyUpAsync(CancellationToken cancellationToken)
    {
        if (!_isRecording || _isProcessing)
        {
            return;
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
                return;
            }

            history.Add(cleaned);
            await clipboardPaster.PasteAsync(cleaned, cancellationToken);
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
