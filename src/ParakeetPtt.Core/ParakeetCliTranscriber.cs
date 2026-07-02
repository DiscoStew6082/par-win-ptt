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
        var request = new ProcessRequest(
            options.CliPath,
            ["transcribe", "--model", options.ModelPath, "--input", wavPath, "--json"],
            options.Timeout ?? TimeSpan.FromMinutes(2));

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
        }

        return new TranscriptResult(text, elapsed, confidence);
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}

public sealed record ProcessRequest(string FileName, IReadOnlyList<string> Arguments, TimeSpan Timeout);

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
            CreateNoWindow = true
        };

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
}
