namespace ParakeetPtt.Core;

public sealed record ModelInfo(
    string Id,
    string DisplayName,
    string LanguageNotes,
    string DecoderDefault,
    string Quantization,
    Uri DownloadUrl,
    string? Sha256,
    long MinimumBytes);

public sealed class ModelRegistry(IReadOnlyList<ModelInfo> models, string defaultModelId)
{
    public const string DefaultModelId = "tdt_ctc-110m-f16";

    public IReadOnlyList<ModelInfo> Models { get; } = models;

    public ModelInfo DefaultModel => Find(defaultModelId)
        ?? throw new InvalidOperationException($"Default model '{defaultModelId}' is not registered.");

    public ModelInfo? Find(string id)
    {
        return Models.FirstOrDefault(model => string.Equals(model.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public static ModelRegistry CreateDefault()
    {
        return new ModelRegistry(
            [
                new ModelInfo(
                    DefaultModelId,
                    "Parakeet TDT+CTC 110M F16",
                    "Fast English dictation default",
                    "tdt_ctc",
                    "F16",
                    new Uri("https://huggingface.co/mudler/parakeet-cpp-gguf/resolve/main/tdt_ctc-110m-f16.gguf"),
                    null,
                    200_000_000),
                new ModelInfo(
                    "tdt-0.6b-v3-f16",
                    "Parakeet TDT 0.6B v3 F16",
                    "Larger multilingual model",
                    "tdt",
                    "F16",
                    new Uri("https://huggingface.co/mudler/parakeet-cpp-gguf/resolve/main/tdt-0.6b-v3-f16.gguf"),
                    null,
                    1_000_000_000)
            ],
            DefaultModelId);
    }
}
