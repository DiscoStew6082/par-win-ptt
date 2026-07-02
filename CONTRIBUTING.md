# Contributing

Thanks for helping improve Parakeet PTT.

## Development Setup

Prerequisites:

- Windows 10 or later
- .NET 8 SDK
- Visual Studio 2022 or another editor with .NET support

Run the test suite before opening a pull request:

```powershell
dotnet test ParakeetPtt.sln
```

Create a local release build with:

```powershell
dotnet publish src\ParakeetPtt.App\ParakeetPtt.App.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64
```

Generated output belongs under `publish\`, `bin\`, or `obj\` and should not be committed.

## Pull Requests

- Keep changes focused.
- Add or update tests for behavior changes.
- Prefer public-interface tests over implementation-detail tests.
- Update `README.md` when behavior, setup, or publishing instructions change.
