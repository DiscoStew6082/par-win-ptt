using System.Diagnostics;
using System.Text.Json;

namespace ParakeetPtt.Core;

public sealed record CliTranscriberOptions(
    string CliPath,
    string ModelPath,
    TimeSpan? Timeout = null);

public sealed class ParakeetCliTranscriber(CliTranscriberOptions options, IProcessRunner processRunner) : ITranscriber
{
    public async Task<TranscriptResult> TranscribeAsync(string wavPath, CancellationToken cancellationToken)
    {
        var cliDirectory = Path.GetDirectoryName(options.CliPath);
        var request = new ProcessRequest(
            options.CliPath,
            ["transcribe", "--model", options.ModelPath, "--input", wavPath, "--json"],
            options.Timeout ?? TimeSpan.FromMinutes(2),
            cliDirectory,
            RuntimePathBuilder.GetRuntimeSearchPaths(options.CliPath));

        var result = await processRunner.RunAsync(request, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"parakeet-cli failed with exit code {result.ExitCode}: {result.StandardError}");
        }

        return Parse(result.StandardOutput, result.Elapsed);
    }

    public static TranscriptResult Parse(string output, TimeSpan elapsed)
    {
        var text = output.Trim();
        double? confidence = null;
        IReadOnlyList<TranscriptWord> words = [];

        if (text.StartsWith('{'))
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            text = ReadString(root, "text")
                ?? ReadString(root, "transcript")
                ?? ReadString(root, "transcription")
                ?? string.Empty;

            if (root.TryGetProperty("confidence", out var confidenceElement)
                && confidenceElement.ValueKind == JsonValueKind.Number
                && confidenceElement.TryGetDouble(out var parsedConfidence))
            {
                confidence = parsedConfidence;
            }

            words = ReadWords(root);
        }

        return new TranscriptResult(text, elapsed, confidence, words);
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static IReadOnlyList<TranscriptWord> ReadWords(JsonElement root)
    {
        if (!root.TryGetProperty("words", out var wordsElement) || wordsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var words = new List<TranscriptWord>();
        foreach (var wordElement in wordsElement.EnumerateArray())
        {
            if (wordElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var text = ReadString(wordElement, "w")
                ?? ReadString(wordElement, "word")
                ?? ReadString(wordElement, "text");
            if (string.IsNullOrWhiteSpace(text)
                || !TryReadSeconds(wordElement, "start", out var start)
                || !TryReadSeconds(wordElement, "end", out var end))
            {
                continue;
            }

            double? confidence = null;
            if (TryReadSeconds(wordElement, "conf", out var parsedConfidence)
                || TryReadSeconds(wordElement, "confidence", out parsedConfidence))
            {
                confidence = parsedConfidence;
            }

            words.Add(new TranscriptWord(text, TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), confidence));
        }

        return words;
    }

    private static bool TryReadSeconds(JsonElement element, string propertyName, out double seconds)
    {
        seconds = 0;
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetDouble(out seconds);
    }
}

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}

public sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    TimeSpan Timeout,
    string? WorkingDirectory = null,
    IReadOnlyList<string>? PathDirectories = null);

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, TimeSpan Elapsed);

public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(request.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var stopwatch = Stopwatch.StartNew();
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            ErrorDialog = false
        };

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            process.StartInfo.WorkingDirectory = request.WorkingDirectory;
        }

        ApplyPathDirectories(process.StartInfo, request.PathDirectories);

        foreach (var argument in request.Arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync(linked.Token);
            var stderr = process.StandardError.ReadToEndAsync(linked.Token);
            await process.WaitForExitAsync(linked.Token);
            stopwatch.Stop();
            return new ProcessResult(process.ExitCode, await stdout, await stderr, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (!process.HasExited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new TimeoutException($"parakeet-cli did not finish within {request.Timeout}.");
        }
    }

    private static void ApplyPathDirectories(ProcessStartInfo startInfo, IReadOnlyList<string>? pathDirectories)
    {
        if (pathDirectories is null || pathDirectories.Count == 0)
        {
            return;
        }

        var existingPath = startInfo.Environment.TryGetValue("PATH", out var path)
            ? path
            : Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        startInfo.Environment["PATH"] = string.Join(Path.PathSeparator, pathDirectories.Concat([existingPath]));
    }
}

public static class RuntimePathBuilder
{
    public static IReadOnlyList<string> GetRuntimeSearchPaths(string executablePath)
    {
        var executableDirectory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(executableDirectory))
        {
            return [];
        }

        var searchPaths = new List<string> { executableDirectory };
        var runtimeRoot = Directory.GetParent(executableDirectory)?.FullName;
        var driveRoot = runtimeRoot is null ? null : Path.GetPathRoot(runtimeRoot);
        if (runtimeRoot is not null
            && Directory.Exists(runtimeRoot)
            && !string.Equals(driveRoot, runtimeRoot, StringComparison.OrdinalIgnoreCase))
        {
            searchPaths.AddRange(Directory.EnumerateDirectories(runtimeRoot, "cudart-*", SearchOption.TopDirectoryOnly));
        }

        return searchPaths
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
