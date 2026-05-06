# AGENTS.md

## Cursor Cloud specific instructions

This is a **single-project WPF .NET 8 Windows desktop application** with no backend services, no database, no containers, and no test projects.

### Building on Linux

The project targets `net8.0-windows` with WPF enabled. To build on this Linux VM, you **must** set the environment variable `EnableWindowsTargeting=true` (already configured in `~/.bashrc`). With this variable set, all standard `dotnet` commands work:

```bash
dotnet restore VpnSpeedAnalyzer.csproj
dotnet build VpnSpeedAnalyzer.csproj -c Debug
dotnet build VpnSpeedAnalyzer.csproj -c Release
dotnet publish VpnSpeedAnalyzer.csproj -c Release -r win-x64 --self-contained true -o out
```

### Linting

```bash
dotnet format --verify-no-changes   # whitespace/style check
dotnet build -warnaserror           # treat warnings as errors
```

Note: The repo has some pre-existing whitespace formatting issues reported by `dotnet format`.

### Tests

There are **no automated test projects** in this repository. Validation is done via successful build + publish.

### Runtime limitations

- The application **cannot run** on Linux (WPF requires Windows). The "hello world" task for this codebase is a successful Debug + Release build and a standalone publish.
- At runtime on Windows, the app requires `speedtest.exe` (Ookla CLI) in PATH for speed measurements.

### Key paths

| Item | Path |
|------|------|
| Project file | `VpnSpeedAnalyzer.csproj` |
| CI workflow | `.github/workflows/build.yml` |
| .NET SDK | `/usr/share/dotnet-latest` (v8.0.420) |
