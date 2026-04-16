using System;
using System.Diagnostics;
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
                var psi = new ProcessStartInfo
                {
                    FileName = "speedtest.exe",
                    Arguments = "--format=json",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(psi);
                string output = await process.StandardOutput.ReadToEndAsync();
                process.WaitForExit();

                using var doc = JsonDocument.Parse(output);
                var root = doc.RootElement;

                return new SpeedtestResult
                {
                    Ping = root.GetProperty("ping").GetProperty("latency").GetDouble(),
                    Jitter = root.GetProperty("ping").GetProperty("jitter").GetDouble(),
                    Loss = root.TryGetProperty("packetLoss", out var lossEl) ? lossEl.GetDouble() : 0,
                    Download = root.GetProperty("download").GetProperty("bandwidth").GetDouble() / 125000.0,
                    Upload = root.GetProperty("upload").GetProperty("bandwidth").GetDouble() / 125000.0,
                    Timestamp = DateTime.Now
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
