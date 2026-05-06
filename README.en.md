# VPN Speed Analyzer

English | [Русский](README.md)

`VPN Speed Analyzer` is a desktop app on `WPF + .NET 8` for monitoring VPN
connection quality and picking the best host based on real network measurements.

## Project at a glance

- Runs periodic measurements via `speedtest.exe` (Ookla CLI) and `ipwho.is` / `ipapi.co`.
- Computes a single quality score `D.Q.S (0..100)` per host for the current usage profile.
- Provides live monitoring (charts, table, top-3 ranking) and a separate analytics view per host.
- Helps you quickly decide which VPN node to use right now.

## Features

- **Real-time monitoring:** ping, jitter, loss, download, upload.
- **Charts:** compact ping and jitter charts with rolling history.
- **Results table:** for every measurement — IP, country, metrics and `D.Q.S`.
- **Host ranking:** top-3 on the `Monitoring` tab + full analytical ranking on the `Analytics` tab.
- **Score explanation:** "Why this D.Q.S?" block explains how the score was built for the selected row.
- **Scoring profiles:** `Universal`, `Gaming`, `Streaming` — change metric weights on the fly.
- **IP / country / ASN detection:** primary `ipwho.is`, fallback `ipapi.co`.
- **Reliable measurements:** `speedtest` retries, UI status reporting, clear errors.
- **CSV export:** full results and top-hosts.
- **Local logs:** stored in `%LOCALAPPDATA%\VpnSpeedAnalyzer\logs` with rotation and cleanup.

## D.Q.S metric

`D.Q.S = Daver Quality Score` — an author-defined unified channel quality score on a `0..100` scale.

Each metric is first normalised to a `0..100` sub-score:

- ping, jitter, loss — *lower is better*: `≤ ideal` → 100, `≥ worst` → 0, linear in between.
- download, upload — *higher is better*: `≥ ideal` → 100, `≤ worst` → 0, linear in between.

Current thresholds:

| Metric        | ideal | worst | What it measures      | Trend            |
| ------------- | ----- | ----- | --------------------- | ---------------- |
| ping (ms)     | 20    | 150   | latency               | lower is better  |
| jitter (ms)   | 2     | 40    | latency stability     | lower is better  |
| loss (%)      | 0     | 5     | packet loss           | lower is better  |
| download      | 400   | 20    | download speed (Mbps) | higher is better |
| upload        | 150   | 10    | upload speed (Mbps)   | higher is better |

The final score is a **weighted sum** of normalised metrics. The weights come from the active profile (see below):

```
D.Q.S = ping_score   * w_ping
      + jitter_score * w_jitter
      + loss_score   * w_loss
      + dl_score     * w_download
      + ul_score     * w_upload
```

Every profile's weights sum to `1.00`, so the result is always in `0..100`.

## Scoring profiles

A profile changes priorities — i.e. metric weights in the `D.Q.S` formula.
The same set of measurements can produce different "best" hosts depending on what you actually do online.

| Profile    | Use case                                              | ping | jitter | loss | download | upload |
| ---------- | ----------------------------------------------------- | ---- | ------ | ---- | -------- | ------ |
| Universal  | Balanced — general internet and VPN usage             | 0.30 | 0.20   | 0.25 | 0.15     | 0.10   |
| Gaming     | Lowest possible latency, jitter and loss              | 0.42 | 0.28   | 0.20 | 0.06     | 0.04   |
| Streaming  | Stable channel + high download/upload speeds          | 0.20 | 0.10   | 0.15 | 0.35     | 0.20   |

How to read this:

- **Gaming:** ping/jitter/loss carry `0.90` of the weight — great speed can't save a host with bad latency.
- **Streaming:** download/upload carry `0.55` of the weight — prefers fast hosts with acceptable latency.
- **Universal:** balanced — sensible default.

The profile switch in the header recomputes the ranking and recommendation instantly.

## Requirements

To get accurate measurements you need to prepare the environment:

- **`speedtest.exe` (Ookla Speedtest CLI).**
  The app runs this binary in background to measure latency and bandwidth.
  Download from the official site: <https://www.speedtest.net/apps/cli>.
  Just place `speedtest.exe` next to the app `.exe` — this folder is checked first.
  Adding it to `PATH` is optional and only used as a fallback if the file is not next to the app.
- **Run everything through the VPN tunnel you are testing.**
  Both the app and `speedtest.exe` must use the same VPN tunnel —
  otherwise external IP, country, ASN and metrics will reflect your real ISP,
  not the VPN. If their traffic bypasses VPN, route and IP changes may not be detected.

## Quick start

1. Clone the repository.
2. Build and run:
   - `dotnet build -c Release`
   - `dotnet run`
3. Place `speedtest.exe` next to the built `.exe` (or add it to `PATH`).
4. Click `Start` in the UI.
5. Switch profiles and compare hosts by `D.Q.S` and raw metrics.

## Release build (Single EXE)

GitHub Actions builds a standalone Windows EXE (`win-x64`) and publishes it as an artifact.

Local publish:

```
dotnet publish VpnSpeedAnalyzer.csproj -c Release -r win-x64 --self-contained true -o out
```

## Technology

- **Platform:** `WPF`, `.NET 8`
- **Charts:** `ScottPlot`
- **Architecture:** MVVM-style (`ViewModels`, `Models`, `Logic`, `Services`)
- **CI/CD:** GitHub Actions
- **Distribution:** single-file standalone EXE

## Logs & privacy

- Logs are stored in `%LOCALAPPDATA%\VpnSpeedAnalyzer\logs` (rotation and auto-cleanup).
- Measurements run on the locally installed `speedtest.exe`. External services are only used for IP / country / ASN detection.
