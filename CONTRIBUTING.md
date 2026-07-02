# Contributing

Thanks for helping improve Parakeet PTT.

## Development Setup

Prerequisites:

- Windows 10 or later
- .NET 10 SDK
- Visual Studio 2026 or another editor with .NET 10 SDK support

The app targets .NET 10 LTS, which is supported until November 14, 2028.

Run the test suite before opening a pull request:

```powershell
dotnet test ParakeetPtt.sln
```

Create a local release build with:

```powershell
dotnet publish src\ParakeetPtt.App\ParakeetPtt.App.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64
```

Before publishing a release, verify the build and record a SHA-256 checksum for the zip:

```powershell
Get-FileHash publish\ParakeetPtt-win-x64.zip -Algorithm SHA256
```

Public releases should also consider code signing, SBOM generation, and provenance attestations.

Generated output belongs under `publish\`, `bin\`, or `obj\` and should not be committed.

## Pull Requests

- Keep changes focused.
- Add or update tests for behavior changes.
- Prefer public-interface tests over implementation-detail tests.
- Update `README.md` when behavior, setup, or publishing instructions change.
