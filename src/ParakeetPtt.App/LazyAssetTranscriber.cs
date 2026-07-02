using ParakeetPtt.Core;

namespace ParakeetPtt.App;

internal sealed class LazyAssetTranscriber(
    string appData,
    AppSettingsStore settingsStore,
    Func<AppSettings> getSettings,
    Action<AppSettings> updateSettings,
    Action<string> reportStatus) : ITranscriber
{
    private readonly SemaphoreSlim _setupLock = new(1, 1);
    private ParakeetCliTranscriber? _inner;

    public async Task<TranscriptResult> TranscribeAsync(string wavPath, CancellationToken cancellationToken)
    {
        var inner = await EnsureInnerAsync(cancellationToken);
        try
        {
            return await inner.TranscribeAsync(wavPath, cancellationToken);
        }
        catch when (getSettings().DevicePreference == DevicePreference.Cuda)
        {
            reportStatus("CUDA transcription failed; retrying with CPU runtime.");
            var settings = getSettings() with
            {
                DevicePreference = DevicePreference.Cpu,
                RuntimePath = null
            };
            updateSettings(settings);
            await settingsStore.SaveAsync(settings, cancellationToken);
            _inner = null;
            inner = await EnsureInnerAsync(cancellationToken);
            return await inner.TranscribeAsync(wavPath, cancellationToken);
        }
    }

    private async Task<ParakeetCliTranscriber> EnsureInnerAsync(CancellationToken cancellationToken)
    {
        if (_inner is not null)
        {
            return _inner;
        }

        await _setupLock.WaitAsync(cancellationToken);
        try
        {
            if (_inner is not null)
            {
                return _inner;
            }

            Directory.CreateDirectory(appData);
            var settings = getSettings();
            var manager = new AssetManager(appData, new HttpFileDownloader());
            var runtimePath = settings.RuntimePath;
            if (string.IsNullOrWhiteSpace(runtimePath) || !File.Exists(runtimePath))
            {
                var runtime = RuntimeAssetRegistry.CreateDefault().For(settings.DevicePreference);
                reportStatus($"Downloading/verifying {runtime.Id} runtime.");
                runtimePath = await manager.EnsureRuntimeAsync(runtime, cancellationToken);
            }

            var modelPath = settings.ModelPath;
            if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
            {
                var registry = ModelRegistry.CreateDefault();
                var model = registry.Find(settings.SelectedModelId) ?? registry.DefaultModel;
                reportStatus($"Downloading/verifying {model.DisplayName}.");
                modelPath = await manager.EnsureModelAsync(model, cancellationToken);
            }

            settings = settings with { RuntimePath = runtimePath, ModelPath = modelPath };
            updateSettings(settings);
            await settingsStore.SaveAsync(settings, cancellationToken);

            _inner = new ParakeetCliTranscriber(
                new CliTranscriberOptions(runtimePath, modelPath, TimeSpan.FromMinutes(5)),
                new SystemProcessRunner());
            return _inner;
        }
        finally
        {
            _setupLock.Release();
        }
    }
}
