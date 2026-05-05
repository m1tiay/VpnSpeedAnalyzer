# VPN Speed Analyzer

`VPN Speed Analyzer` is a desktop app on `WPF + .NET 8` that helps you quickly find the best VPN host based on real measurements, not guesswork.

The app monitors network state, runs speed tests, calculates a unified quality score, and provides clear recommendations for the optimal host right now.

## TL;DR

Fast VPN host selection using live metrics: ping, jitter, packet loss, download/upload, and `QualityScore (0..100)` with profile-based ranking.

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

## Why This Project

Choosing a VPN endpoint by country or provider name often leads to unstable latency and speed.  
`VPN Speed Analyzer` solves this by turning raw network checks into actionable rankings and recommendations.

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

## Build Release EXE

GitHub Actions pipeline builds a standalone single EXE for Windows (`win-x64`).

You can also publish locally:

`dotnet publish VpnSpeedAnalyzer.csproj -c Release -r win-x64 --self-contained true -o out`

## Roadmap Ideas

- Advanced ranking filters (time ranges, profile snapshots)
- Historical trend analytics
- Optional notifications on host quality drops

---

Built to make VPN quality visible, measurable, and easy to optimize.
