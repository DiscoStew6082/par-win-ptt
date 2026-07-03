using ParakeetPtt.Core;

namespace ParakeetPtt.App;

internal static class AudioChunkPublisher
{
    public static void Publish(
        PendingAudioChunk chunk,
        Action<RecordedAudio>? handler,
        Action<string, byte[]> writeWav,
        Action<string> delete)
    {
        try
        {
            writeWav(chunk.Path, chunk.Pcm);
            if (handler is null)
            {
                delete(chunk.Path);
                return;
            }

            handler.Invoke(new RecordedAudio(chunk.Path, chunk.Duration, DeleteAfterUse: true));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
