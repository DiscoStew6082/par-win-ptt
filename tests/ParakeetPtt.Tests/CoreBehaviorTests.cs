using ParakeetPtt.Core;
using System.IO.Compression;

namespace ParakeetPtt.Tests;

[TestClass]
public sealed class CoreBehaviorTests
{
    [TestMethod]
    public async Task ReleaseTranscribesAndPastesCleanedText()
    {
        var recorder = new FakeAudioRecorder("utterance.wav");
        var transcriber = new FakeTranscriber("  hello parakeet  \n\n");
        var paster = new FakeClipboardPaster();
        var controller = new DictationController(recorder, transcriber, paster, new SessionHistory());

        var started = await controller.HandleHotkeyDownAsync(CancellationToken.None);
        var outcome = await controller.HandleHotkeyUpAsync(CancellationToken.None);

        Assert.IsTrue(started);
        Assert.AreEqual(DictationOutcome.Pasted, outcome);
        Assert.AreEqual(1, recorder.StartCount);
        Assert.AreEqual(1, recorder.StopCount);
        Assert.AreEqual("utterance.wav", transcriber.LastAudioPath);
        Assert.AreEqual("Hello parakeet.", paster.PastedText);
    }

    [TestMethod]
    public async Task ReleasePublishesCleanedTranscriptPreviewBeforePaste()
    {
        string? preview = null;
        string? previewAtPaste = null;
        var paster = new FakeClipboardPaster(() => previewAtPaste = preview);
        var controller = new DictationController(
            new FakeAudioRecorder("utterance.wav"),
            new FakeTranscriber("  preview this  "),
            paster,
            new SessionHistory(),
            text => preview = text);

        await controller.HandleHotkeyDownAsync(CancellationToken.None);
        var outcome = await controller.HandleHotkeyUpAsync(CancellationToken.None);

        Assert.AreEqual(DictationOutcome.Pasted, outcome);
        Assert.AreEqual("Preview this.", preview);
        Assert.AreEqual("Preview this.", previewAtPaste);
    }

    [TestMethod]
    public async Task PreviewFailureDoesNotBlockPaste()
    {
        var paster = new FakeClipboardPaster();
        var controller = new DictationController(
            new FakeAudioRecorder("utterance.wav"),
            new FakeTranscriber("still paste this"),
            paster,
            new SessionHistory(),
            _ => throw new InvalidOperationException("preview failed"));

        await controller.HandleHotkeyDownAsync(CancellationToken.None);
        var outcome = await controller.HandleHotkeyUpAsync(CancellationToken.None);

        Assert.AreEqual(DictationOutcome.Pasted, outcome);
        Assert.AreEqual("Still paste this.", paster.PastedText);
    }

    [TestMethod]
    public async Task DuplicateKeydownDoesNotStartSecondRecording()
    {
        var recorder = new FakeAudioRecorder("utterance.wav");
        var controller = new DictationController(
            recorder,
            new FakeTranscriber("hello"),
            new FakeClipboardPaster(),
            new SessionHistory());

        var firstStarted = await controller.HandleHotkeyDownAsync(CancellationToken.None);
        var duplicateStarted = await controller.HandleHotkeyDownAsync(CancellationToken.None);
        var outcome = await controller.HandleHotkeyUpAsync(CancellationToken.None);

        Assert.IsTrue(firstStarted);
        Assert.IsFalse(duplicateStarted);
        Assert.AreEqual(DictationOutcome.Pasted, outcome);
        Assert.AreEqual(1, recorder.StartCount);
        Assert.AreEqual(1, recorder.StopCount);
    }

    [TestMethod]
    public async Task EmptyTranscriptDoesNotPasteOrEnterHistory()
    {
        var history = new SessionHistory();
        var paster = new FakeClipboardPaster();
        var controller = new DictationController(
            new FakeAudioRecorder("utterance.wav"),
            new FakeTranscriber("   "),
            paster,
            history);

        await controller.HandleHotkeyDownAsync(CancellationToken.None);
        var outcome = await controller.HandleHotkeyUpAsync(CancellationToken.None);

        Assert.AreEqual(DictationOutcome.EmptyTranscript, outcome);
        Assert.IsNull(paster.PastedText);
        Assert.AreEqual(0, history.Items.Count);
    }

    [TestMethod]
    public async Task TemporaryRecordingIsDeletedAfterTranscription()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"parakeet-ptt-{Guid.NewGuid():N}.wav");
        await File.WriteAllTextAsync(tempPath, "fake wav");
        var controller = new DictationController(
            new FakeAudioRecorder(tempPath, deleteAfterUse: true),
            new FakeTranscriber("hello"),
            new FakeClipboardPaster(),
            new SessionHistory());

        await controller.HandleHotkeyDownAsync(CancellationToken.None);
        var outcome = await controller.HandleHotkeyUpAsync(CancellationToken.None);

        Assert.AreEqual(DictationOutcome.Pasted, outcome);
        Assert.IsFalse(File.Exists(tempPath));
    }

    [TestMethod]
    public async Task TemporaryRecordingDeleteFailurePublishesCleanupWarning()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"parakeet-ptt-{Guid.NewGuid():N}.wav");
        await File.WriteAllTextAsync(tempPath, "fake wav");
        File.SetAttributes(tempPath, FileAttributes.ReadOnly);
        string? warningPath = null;
        var controller = new DictationController(
            new FakeAudioRecorder(tempPath, deleteAfterUse: true),
            new FakeTranscriber("hello"),
            new FakeClipboardPaster(),
            new SessionHistory(),
            cleanupWarningReady: path => warningPath = path);

        var outcome = await controller.HandleHotkeyUpAsync(CancellationToken.None);

        Assert.AreEqual(DictationOutcome.NotRecording, outcome);
        Assert.IsNull(warningPath);

        await controller.HandleHotkeyDownAsync(CancellationToken.None);
        outcome = await controller.HandleHotkeyUpAsync(CancellationToken.None);

        Assert.AreEqual(DictationOutcome.Pasted, outcome);
        Assert.AreEqual(tempPath, warningPath);
        File.SetAttributes(tempPath, FileAttributes.Normal);
        File.Delete(tempPath);
    }

    [TestMethod]
    public async Task ParakeetCliTranscriberConstructsCommandAndParsesJson()
    {
        var runner = new FakeProcessRunner("""{"text":"hello from cli","duration":1.25,"confidence":0.88}""");
        var transcriber = new ParakeetCliTranscriber(
            new CliTranscriberOptions("C:\\tools\\parakeet-cli.exe", "C:\\models\\tdt_ctc-110m-f16.gguf"),
            runner);

        var result = await transcriber.TranscribeAsync("C:\\temp\\speech.wav", CancellationToken.None);

        Assert.AreEqual("hello from cli", result.Text);
        Assert.AreEqual(0.88, result.Confidence);
        Assert.AreEqual("C:\\tools\\parakeet-cli.exe", runner.LastRequest?.FileName);
        Assert.AreEqual("C:\\tools", runner.LastRequest?.WorkingDirectory);
        CollectionAssert.AreEqual(
            new[] { "transcribe", "--model", "C:\\models\\tdt_ctc-110m-f16.gguf", "--input", "C:\\temp\\speech.wav", "--json" },
            runner.LastRequest?.Arguments.ToArray());
    }

    [TestMethod]
    public void RuntimePathBuilderIncludesSiblingCudaDllDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"parakeet-runtime-paths-{Guid.NewGuid():N}");
        var cliDir = Path.Combine(root, "parakeet-v0.4.0-bin-win-cuda-x64");
        var cudaDir = Path.Combine(root, "cudart-parakeet-bin-win-cuda-x64");
        Directory.CreateDirectory(cliDir);
        Directory.CreateDirectory(cudaDir);
        var cliPath = Path.Combine(cliDir, "parakeet-cli.exe");
        File.WriteAllText(cliPath, string.Empty);

        var paths = RuntimePathBuilder.GetRuntimeSearchPaths(cliPath);

        CollectionAssert.Contains(paths.ToArray(), cliDir);
        CollectionAssert.Contains(paths.ToArray(), cudaDir);
        Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    public async Task ProcessRunnerPreservesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var runner = new SystemProcessRunner();
        var request = new ProcessRequest(
            "cmd.exe",
            ["/c", "ping", "127.0.0.1", "-n", "6"],
            TimeSpan.FromSeconds(30));

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => runner.RunAsync(request, cancellation.Token));
    }

    [TestMethod]
    public async Task SettingsRoundtripKeepsSelectedModelAndDevicePreference()
    {
        var path = Path.Combine(Path.GetTempPath(), $"parakeet-settings-{Guid.NewGuid():N}.json");
        var store = new AppSettingsStore(path);
        var saved = AppSettings.Default with
        {
            SelectedModelId = "tdt-0.6b-v3-f16",
            DevicePreference = DevicePreference.Cpu,
            NotificationsEnabled = false,
            AudibleStatusEnabled = false
        };

        await store.SaveAsync(saved, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(saved.SelectedModelId, loaded.SelectedModelId);
        Assert.AreEqual(DevicePreference.Cpu, loaded.DevicePreference);
        Assert.IsFalse(loaded.NotificationsEnabled);
        Assert.IsFalse(loaded.AudibleStatusEnabled);
        File.Delete(path);
    }

    [TestMethod]
    public void ModelRegistryExposesCuratedDefaultAndMultilingualModel()
    {
        var registry = ModelRegistry.CreateDefault();
        var multilingual = registry.Find("tdt-0.6b-v3-f16");

        Assert.AreEqual("tdt_ctc-110m-f16", registry.DefaultModel.Id);
        Assert.IsNotNull(multilingual);
        StringAssert.Contains(registry.DefaultModel.DownloadUrl.ToString(), "tdt_ctc-110m-f16.gguf");
        Assert.IsTrue(registry.DefaultModel.MinimumBytes > 0);
        Assert.AreEqual("7f9a6376edde6a74592ace48b2ebdc27a1ac972d0be9dfcc29e668d99381faf1", registry.DefaultModel.Sha256);
        Assert.AreEqual("8ba47343e1e919895aca90e099150a01ed203ee0942d8ed31e27295efc5abb22", multilingual?.Sha256);
    }

    [TestMethod]
    public async Task ChecksumVerifierRejectsMismatchedSha256()
    {
        var path = Path.Combine(Path.GetTempPath(), $"parakeet-checksum-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "hello");

        Assert.IsTrue(await ChecksumVerifier.VerifySha256Async(path, "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", CancellationToken.None));
        Assert.IsFalse(await ChecksumVerifier.VerifySha256Async(path, "0000000000000000000000000000000000000000000000000000000000000000", CancellationToken.None));
        File.Delete(path);
    }

    [TestMethod]
    public void RuntimeRegistryPinsLatestWindowsCudaAndCpuAssets()
    {
        var registry = RuntimeAssetRegistry.CreateDefault();

        Assert.AreEqual("v0.4.0", registry.ReleaseTag);
        StringAssert.EndsWith(registry.Cuda.DownloadUrl.ToString(), "parakeet-v0.4.0-bin-win-cuda-x64.zip");
        Assert.AreEqual("2a377eeb7f92e0d0cd28df768750a8132296de0c07454ad908d5eaceeb9ad5e4", registry.Cuda.Sha256);
        Assert.AreEqual(1, registry.Cuda.AdditionalArchives.Count);
        StringAssert.EndsWith(registry.Cuda.AdditionalArchives[0].DownloadUrl.ToString(), "cudart-parakeet-bin-win-cuda-x64.zip");
        Assert.AreEqual("cc2b5fb99951720130e4a701e0978419d0a878e25c88bebc1416152616bd1d94", registry.Cuda.AdditionalArchives[0].Sha256);
        StringAssert.EndsWith(registry.Cpu.DownloadUrl.ToString(), "parakeet-v0.4.0-bin-win-cpu-x64.zip");
    }

    [TestMethod]
    public void DictationStatusCatalogProvidesVisibleTranscribingState()
    {
        var status = DictationStatusCatalog.Transcribing;

        Assert.AreEqual(DictationStatusKind.Transcribing, status.Kind);
        Assert.AreEqual("Transcribing", status.Title);
        Assert.AreEqual("Sending audio to local parakeet-cli.", status.Message);
        Assert.IsFalse(status.AutoHide);
    }

    [TestMethod]
    public void DictationStatusCatalogProvidesTranscriptPreviewText()
    {
        var status = DictationStatusCatalog.TranscriptPreview("Preview this.");

        Assert.AreEqual(DictationStatusKind.TranscriptPreview, status.Kind);
        Assert.AreEqual("Transcript", status.Title);
        Assert.AreEqual("Preview this.", status.Message);
        Assert.IsFalse(status.AutoHide);
    }

    [TestMethod]
    public async Task AssetManagerDownloadsVerifiesExtractsRuntimeAndFindsCli()
    {
        var root = Path.Combine(Path.GetTempPath(), $"parakeet-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "runtime.zip");
        CreateRuntimeZip(zipPath);
        var sha = await ChecksumVerifier.ComputeSha256Async(zipPath, CancellationToken.None);
        var downloader = new FakeDownloader(await File.ReadAllBytesAsync(zipPath));
        var manager = new AssetManager(root, downloader);
        var runtime = new RuntimeAssetInfo(
            "test-runtime",
            new Uri("https://example.invalid/runtime.zip"),
            sha,
            "runtime.zip",
            DevicePreference.Cuda);

        var cliPath = await manager.EnsureRuntimeAsync(runtime, CancellationToken.None);

        Assert.IsTrue(File.Exists(cliPath));
        StringAssert.EndsWith(cliPath, "parakeet-cli.exe");
        Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    public async Task AssetManagerRevalidatesExtractedRuntimeFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"parakeet-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "runtime.zip");
        CreateRuntimeZip(zipPath);
        var sha = await ChecksumVerifier.ComputeSha256Async(zipPath, CancellationToken.None);
        var manager = new AssetManager(root, new FakeDownloader(await File.ReadAllBytesAsync(zipPath)));
        var runtime = new RuntimeAssetInfo(
            "test-runtime",
            new Uri("https://example.invalid/runtime.zip"),
            sha,
            "runtime.zip",
            DevicePreference.Cuda);
        var cliPath = await manager.EnsureRuntimeAsync(runtime, CancellationToken.None);
        await File.WriteAllTextAsync(cliPath, "tampered");

        var repairedCliPath = await manager.EnsureRuntimeAsync(runtime, CancellationToken.None);

        Assert.AreEqual(cliPath, repairedCliPath);
        Assert.AreEqual("fake exe", await File.ReadAllTextAsync(repairedCliPath));
        Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    public async Task AssetManagerRejectsRuntimeArchiveEntriesOutsideRuntimeDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"parakeet-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "runtime.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../evil.txt");
            await using var stream = entry.Open();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync("escape");
        }

        var sha = await ChecksumVerifier.ComputeSha256Async(zipPath, CancellationToken.None);
        var manager = new AssetManager(root, new FakeDownloader(await File.ReadAllBytesAsync(zipPath)));
        var runtime = new RuntimeAssetInfo(
            "test-runtime",
            new Uri("https://example.invalid/runtime.zip"),
            sha,
            "runtime.zip",
            DevicePreference.Cuda);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => manager.EnsureRuntimeAsync(runtime, CancellationToken.None));
        Assert.IsFalse(File.Exists(Path.Combine(root, "evil.txt")));
        Directory.Delete(root, recursive: true);
    }

    private static void CreateRuntimeZip(string zipPath)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("bin/parakeet-cli.exe");
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write("fake exe");
    }
}

internal sealed class FakeAudioRecorder(string path, bool deleteAfterUse = false) : IAudioRecorder
{
    public int StartCount { get; private set; }
    public int StopCount { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        StartCount++;
        return Task.CompletedTask;
    }

    public Task<RecordedAudio> StopAsync(CancellationToken cancellationToken)
    {
        StopCount++;
        return Task.FromResult(new RecordedAudio(path, TimeSpan.FromSeconds(1), deleteAfterUse));
    }
}

internal sealed class FakeTranscriber(string transcript) : ITranscriber
{
    public string? LastAudioPath { get; private set; }

    public Task<TranscriptResult> TranscribeAsync(string wavPath, CancellationToken cancellationToken)
    {
        LastAudioPath = wavPath;
        return Task.FromResult(new TranscriptResult(transcript, TimeSpan.FromMilliseconds(500), null));
    }
}

internal sealed class FakeClipboardPaster(Action? beforePaste = null) : IClipboardPaster
{
    public string? PastedText { get; private set; }

    public Task PasteAsync(string text, CancellationToken cancellationToken)
    {
        beforePaste?.Invoke();
        PastedText = text;
        return Task.CompletedTask;
    }
}

internal sealed class FakeProcessRunner(string standardOutput) : IProcessRunner
{
    public ProcessRequest? LastRequest { get; private set; }

    public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(new ProcessResult(0, standardOutput, string.Empty, TimeSpan.FromMilliseconds(10)));
    }
}

internal sealed class FakeDownloader(byte[] bytes) : IFileDownloader
{
    public Task DownloadAsync(Uri source, string destinationPath, CancellationToken cancellationToken)
    {
        File.WriteAllBytes(destinationPath, bytes);
        return Task.CompletedTask;
    }
}
