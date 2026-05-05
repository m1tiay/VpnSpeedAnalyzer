# VPN Speed Analyzer

English | [Русский](README.md)

`VPN Speed Analyzer` is a desktop app on `WPF + .NET 8` for selecting the best VPN host using real network measurements.

## TL;DR

Real-time host ranking by ping, jitter, packet loss, download/upload, and `QualityScore (0..100)` with profile presets.

## Key Features

- **Smart IP detection:** primary source `ipwho.is`, fallback `ipapi.co`.
- **Reliable measurements:** speedtest retries, error reason logging, and UI status reporting.
- **Quality scoring:** host score from `0..100` with profile presets:
  - `Universal`
  - `Gaming`
  - `Streaming`
- **Host rating screen:** top hosts list + best host recommendation.
- **Explainable score:** "Why this score?" block for selected result.
- **Live visualization:** compact ping and jitter charts.
- **CSV export:** full results export and top-hosts export.
- **Local logging:** logs stored in `%LOCALAPPDATA%\VpnSpeedAnalyzer\logs` with rotation and cleanup.

## Why It Helps

Selecting a VPN by country/provider alone often gives unstable latency and throughput.
This project turns raw measurements into a practical host ranking with clear recommendations.

## Technology

- **Platform:** `WPF`, `.NET 8`
- **Charts:** `ScottPlot`
- **Architecture:** MVVM-style (`ViewModels`, `Models`, `Logic`, `Services`)
- **CI/CD:** GitHub Actions
- **Distribution:** single-file standalone EXE publish

## Quick Start

1. Clone repository.
2. Build and run:
   - `dotnet build -c Release`
   - `dotnet run`
3. Start monitoring in UI (`Start` button).
4. Switch scoring profile (`Universal/Gaming/Streaming`) and compare top hosts.

## Release Build (Single EXE)

GitHub Actions builds a standalone Windows EXE (`win-x64`) and uploads it as an artifact.

You can also publish locally:

`dotnet publish VpnSpeedAnalyzer.csproj -c Release -r win-x64 --self-contained true -o out`

## Notes

- Logs are stored in `%LOCALAPPDATA%\VpnSpeedAnalyzer\logs`.
- App supports dark UI and status-based monitoring feedback.

---

For Russian documentation, open [`README.md`](README.md).
