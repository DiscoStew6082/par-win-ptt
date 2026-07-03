# Parakeet PTT

[![CI](https://github.com/discostew6082/par-win-ptt/actions/workflows/ci.yml/badge.svg)](https://github.com/discostew6082/par-win-ptt/actions/workflows/ci.yml)

Parakeet PTT is a local Windows push-to-talk dictation tray app. Hold Right Ctrl, speak, release, and the app records a temporary 16 kHz mono WAV, transcribes it with `parakeet-cli`, normalizes the transcript, pastes it into the active app, and restores your previous clipboard when possible.

## Features

- Right Ctrl push-to-talk dictation from the system tray.
- Optional Right Shift toggle dictation mode.
- Local transcription with downloadable Parakeet runtime/model assets.
- Session-only transcript history.
- Runtime/model path overrides for local experimentation.
- Dark-mode-first Windows Forms UI.

## Privacy

Parakeet PTT is designed for local dictation. Temporary recordings are made on the local machine while dictation is active, transcription is performed by a local `parakeet-cli` runtime, and transcript history is session-only. The app does download runtime/model assets on first use unless you configure local paths in settings.

Trust-boundary notes:

- Paste is implemented through the Windows clipboard. The app temporarily places the transcript on the clipboard, sends paste to the active window, and attempts to restore the previous clipboard contents afterward. Other local apps with clipboard access may observe clipboard contents while paste is in progress.
- Runtime and model path overrides execute the local files you select. Use overrides only for runtimes and models from sources you trust.
- The push-to-talk hotkey uses a low-level Windows keyboard hook so the app can detect Right Ctrl while it is running. The hook is used for hotkey state, not transcript collection.
- Runtime/model downloads leave the local machine to fetch third-party artifacts; transcription itself runs locally.

## Requirements

- Windows 11, or Windows 10 installations still receiving security updates.
- .NET 10 SDK for development.
- A working audio input device.

Supported releases target Windows 10/11 on x64. The app targets .NET 10 LTS, which is supported until November 14, 2028.

## Build

Run the tests:

```powershell
dotnet test ParakeetPtt.sln
```

Publish a portable Windows x64 build:

```powershell
dotnet publish src\ParakeetPtt.App\ParakeetPtt.App.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64
```

Package the published folder as a zip:

```powershell
Compress-Archive -Path publish\win-x64\* -DestinationPath publish\ParakeetPtt-win-x64.zip -Force
```

Create a checksum for release verification:

```powershell
Get-FileHash publish\ParakeetPtt-win-x64.zip -Algorithm SHA256
```

## Try It Locally

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

Expect first-run downloads to be hundreds of MB for the default model and runtime assets. The optional larger multilingual model is about 1.4 GB.

Open the tray menu for settings, session-only transcript history, and runtime/model path overrides.

## Downloaded Assets

This repository is licensed under MIT. The runtime and model assets downloaded on first use are third-party artifacts from their upstream projects:

- `parakeet.cpp` runtime archives are downloaded from the `mudler/parakeet.cpp` GitHub release `v0.4.0`. Runtime archives are verified with pinned SHA-256 hashes before extraction.
- GGUF model files are downloaded from `mudler/parakeet-cpp-gguf` on Hugging Face. Built-in model downloads are checked for minimum expected size and pinned SHA-256 hashes; configure a local model path in settings only when you trust that local model file.

Review the upstream repositories for their own license terms before redistributing bundled runtime or model assets.

## Release Verification

Every published release should include a SHA-256 checksum for the downloadable zip. Users should compare the published checksum with:

```powershell
Get-FileHash .\ParakeetPtt-win-x64.zip -Algorithm SHA256
```

Release builds from this repository publish the zip, checksum, and CycloneDX SBOM. Public tag builds also create a GitHub artifact attestation. Tag builds create a draft GitHub Release so maintainers can review assets before publishing. Recommended additional hardening for broad public distribution includes code signing.

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

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and pull request guidance.

## License

Parakeet PTT is available under the [MIT License](LICENSE).
