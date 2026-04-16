using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace VpnSpeedAnalyzer
{
    public class SpeedtestService
    {
        public async Task<SpeedtestResult> RunAsync()
        {
            try
            {
                Log.Info("Starting speedtest...");

                var psi = new ProcessStartInfo
                {
                    FileName = "speedtest.exe",
                    Arguments = "-f json --accept-license --accept-gdpr",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };

                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (!string.IsNullOrWhiteSpace(error))
                    Log.Error("Speedtest stderr: " + error);

                if (string.IsNullOrWhiteSpace(output))
                {
                    Log.Error("Speedtest output is empty");
                    return null;
                }

                Log.Info("Speedtest raw output: " + output);

                using var doc = JsonDocument.Parse(output);
                var root = doc.RootElement;

                var ping = root.GetProperty("ping").GetProperty("latency").GetDouble();
                var jitter = root.GetProperty("ping").GetProperty("jitter").GetDouble();
                var packetLoss = root.TryGetProperty("packetLoss", out var lossProp)
                    ? lossProp.GetDouble()
                    : 0;

                var download = root.GetProperty("download").GetProperty("bandwidth").GetDouble() * 8 / 1_000_000;
                var upload = root.GetProperty("upload").GetProperty("bandwidth").GetDouble() * 8 / 1_000_000;

                var result = new SpeedtestResult
                {
                    Ping = ping,
                    Jitter = jitter,
                    Loss = packetLoss,
                    Download = download,
                    Upload = upload,
                    Timestamp = DateTime.Now
                };

                Log.Info($"Parsed speedtest result: ping={ping}, jitter={jitter}, dl={download}, ul={upload}");

                return result;
            }
            catch (Exception ex)
            {
                Log.Error("Speedtest exception: " + ex);
                return null;
            }
        }
    }
}
