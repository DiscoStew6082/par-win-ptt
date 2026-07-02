# Parakeet PTT

Parakeet PTT is a local Windows push-to-talk dictation tray app. Hold Right Ctrl, speak, release, and the app records a temporary 16 kHz mono WAV, transcribes it with `parakeet-cli`, normalizes the transcript, pastes it into the active app, and restores your previous clipboard when possible.

## Try It

Build output is published to:

```text
publish\win-x64\ParakeetPtt.App.exe
```

Portable zip:

```text
publish\ParakeetPtt-win-x64.zip
```

On first real use the app downloads assets under `%LOCALAPPDATA%\ParakeetPtt`:

- `parakeet.cpp` `v0.4.0` Windows CUDA runtime plus the matching CUDA runtime dependency archive.
- CPU fallback runtime.
- Default `tdt_ctc-110m-f16.gguf` model from `mudler/parakeet-cpp-gguf`.

Open the tray menu for settings, session-only transcript history, and runtime/model path overrides.

## Validation

Run:

```powershell
dotnet test ParakeetPtt.sln
dotnet publish src\ParakeetPtt.App\ParakeetPtt.App.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64
```

Real smoke test performed with `parakeet-v0.4.0-bin-win-cpu-x64.zip` and `tdt_ctc-110m-f16.gguf` against a generated speech WAV:

```json
{"text":"Hello parakeet push to talk."}
```
