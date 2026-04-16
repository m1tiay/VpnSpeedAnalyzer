using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using VpnSpeedAnalyzer.Models;

namespace VpnSpeedAnalyzer.Services
{
    public class SpeedtestService
    {
        private readonly IpInfoService _ip = new();

        public async Task<SpeedtestResult?> RunAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "speedtest",
                    Arguments = "--format=json",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                    return null;

                string output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                var raw = JsonSerializer.Deserialize<SpeedtestRaw>(output);
                if (raw == null)
                    return null;

                var ipInfo = await _ip.GetCurrentAsync();

                return new SpeedtestResult
                {
                    Ip = ipInfo?.Ip ?? "",
                    Country = ipInfo?.CountryName ?? "",
                    Timestamp = DateTime.Now,
                    Ping = raw.Ping.Latency,
                    Jitter = raw.Ping.Jitter,
                    Loss = raw.PacketLoss,
                    Download = raw.Download.Bandwidth / 125000.0,
                    Upload = raw.Upload.Bandwidth / 125000.0
                };
            }
            catch
            {
                return null;
            }
        }
    }

    public class SpeedtestRaw
    {
        public PingData Ping { get; set; } = new();
        public BandwidthData Download { get; set; } = new();
        public BandwidthData Upload { get; set; } = new();
        public double PacketLoss { get; set; }

        public class PingData
        {
            public double Latency { get; set; }
            public double Jitter { get; set; }
        }

        public class BandwidthData
        {
            public double Bandwidth { get; set; }
        }
    }
}
