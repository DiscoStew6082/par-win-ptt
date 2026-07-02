# Security Policy

Parakeet PTT is a local Windows tray application. It records temporary audio only while dictation is active and uses local runtime/model assets for transcription.

## Supported Versions

Security fixes are intended for the latest release only. Supported builds target Windows 10/11 x64 and .NET 10 LTS, which is supported until November 14, 2028.

## Trust Boundaries

- Clipboard paste is a local OS boundary. Transcripts are temporarily placed on the clipboard and pasted into the active app; clipboard restoration is best-effort.
- Runtime and model path overrides trust the selected local files. Do not point the app at untrusted executables or models.
- The low-level keyboard hook is used to detect the push-to-talk hotkey while the app is running.
- Runtime/model downloads contact upstream hosts on first use unless local paths are configured.

Release artifacts should publish SHA-256 checksums at minimum. Code signing, SBOMs, and provenance attestations are recommended for public distribution.

## Reporting a Vulnerability

Please report security concerns privately before opening a public issue.

Before this repository is made public, enable GitHub private vulnerability reporting in the repository security settings. Once enabled, use that channel for vulnerability reports.

If private vulnerability reporting is not available yet, contact the maintainer out of band and do not include exploit details, sensitive audio, transcripts, credentials, or proof-of-concept payloads in a public issue.

Include:

- A short description of the issue.
- Steps to reproduce it.
- Any logs, screenshots, or proof-of-concept details that are safe to share.

Please do not include sensitive personal audio, transcripts, or credentials in reports.
