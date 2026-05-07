# VPN Speed Analyzer

English | [Русский](README.md)

`VPN Speed Analyzer` is a desktop app built with `WPF + .NET 8` for monitoring
VPN connection quality and choosing the best host based on real measurements.

## Quick guide (user-focused)

1. Download the app and run it on Windows.
2. Download `speedtest.exe` (Ookla CLI): <https://www.speedtest.net/apps/cli>.
3. Place `speedtest.exe` next to `VpnSpeedAnalyzer.exe` (recommended).
4. Make sure **both `VpnSpeedAnalyzer.exe` and `speedtest.exe` use the same VPN tunnel** you want to evaluate.
5. Click `Start` and wait for the first successful measurement.

If `speedtest.exe` is next to `VpnSpeedAnalyzer.exe` and both go through the same VPN, no extra setup is usually required.

## Core application logic

- The app measures: `ping`, `jitter`, `loss`, `download`, `upload`.
- It calculates `D.Q.S` (`0..100`) and ranks hosts.
- Higher `D.Q.S` means a better host for the selected profile.
- `Monitoring` shows current top results, `Analytics` shows full host summary.

### How D.Q.S is calculated

`D.Q.S` is the final host quality score on a `0..100` scale.

The logic is straightforward:

1. The app takes 5 metrics: `ping`, `jitter`, `loss`, `download`, `upload`.
2. For each metric it determines "how good it is":
   - `ping`, `jitter`, `loss` — lower is better;
   - `download`, `upload` — higher is better.
3. Then it combines them into one `D.Q.S` score.

Reference thresholds used in scoring:

| Metric      | ideal | worst | What it measures      | Trend            |
| ----------- | ----- | ----- | --------------------- | ---------------- |
| ping (ms)   | 20    | 150   | latency               | lower is better  |
| jitter (ms) | 2     | 40    | latency stability     | lower is better  |
| loss (%)    | 0     | 5     | packet loss           | lower is better  |
| download    | 400   | 20    | download speed (Mbps) | higher is better |
| upload      | 150   | 10    | upload speed (Mbps)   | higher is better |

Why the same host can get different final results in different modes:

- profile changes metric importance (their contribution to final score);
- raw measurements stay the same, only priority changes.

How to read score ranges:

- `90+` — excellent host for the selected scenario;
- `70-90` — stable and usable;
- below `70` — compare against other hosts in the table.

## Profile logic

A profile answers: **"what matters most right now?"**  
Switching profile does not alter raw values — it changes priorities and instantly rebuilds ranking.

Simple principle:

- if you want balance — hosts with no obvious weak points go up;
- if you want responsiveness — hosts with better latency go up;
- if you want speed — hosts with better throughput go up.

In the UI:

- `Universal` — balanced mode.
- `Gaming` — prioritises low latency and packet loss.
- `Streaming` — prioritises stability and throughput.

What changes when profile changes:

- metric "weight" in `D.Q.S` changes;
- one host can be #1 in one profile and lower in another;
- raw `ping/jitter/loss/download/upload` values are unchanged — only priorities shift.

Simple example:

- in `Universal`, a host with balanced metrics usually wins;
- in `Gaming`, low-ping hosts rise even with slightly lower throughput;
- in `Streaming`, high-throughput hosts can lead if latency remains acceptable.

In practice:

- switch profile based on your current task;
- see how the leader changes;
- choose host per scenario (gaming, streaming, everyday use), not "once forever".

## VPN host-switch detection logic

The app tracks transport fingerprint of VPN process traffic (PID-based):

- changes are measured via endpoint `delta`;
- `debounce` (~2.5s) filters short noise;
- events are classified as:
  - `PRIMARY` — large real switch (manual or auto-server);
  - `TAIL` — internal follow-up reshaping.

**Auto-measurement is triggered only for `PRIMARY`.**  
This keeps real switch detection while reducing false triggers.

## Main features in one place

- Real-time VPN quality monitoring.
- Host ranking and analytics by `D.Q.S`.
- Scenario profiles (`Universal`, `Gaming`, `Streaming`).
- Automatic VPN host-switch detection with false-trigger protection.
- CSV export (all measurements and top hosts).

## Release build (Single EXE)

GitHub Actions publishes standalone Windows EXE (`win-x64`) as an artifact.

Local publish command:

```
dotnet publish VpnSpeedAnalyzer.csproj -c Release -r win-x64 --self-contained true -o out
```

## Technology

- **Platform:** `WPF`, `.NET 8`
- **Charts:** `ScottPlot`
- **Architecture:** MVVM (`ViewModels`, `Models`, `Logic`, `Services`)
- **CI/CD:** GitHub Actions
- **Distribution:** standalone single EXE

## Logs & privacy

- Logs: `%LOCALAPPDATA%\VpnSpeedAnalyzer\logs` (briefly: one file per run, auto-cleanup).
- Measurements run via local `speedtest.exe`; external services are used only for IP / country / ASN detection.

## Third-party licensing

- `speedtest.exe` (Ookla Speedtest CLI) is a third-party component owned by Ookla.
- Users download it separately from the official website: <https://www.speedtest.net/apps/cli>.
- Use of `speedtest.exe` is governed by Ookla license/terms; this app does not modify or repackage the binary.
