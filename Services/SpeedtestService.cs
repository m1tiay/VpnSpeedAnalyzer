using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VpnSpeedAnalyzer.Models;

namespace VpnSpeedAnalyzer.Services
{
    public class SpeedtestService
    {
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

                return new SpeedtestResult
                {
                    Ip = raw.Interface.ExternalIp ?? "",
                    Country = raw.Server.Country ?? "",
                    Timestamp = DateTime.Now,
                    Ping = raw.Ping.Latency,
                    Jitter = raw.Ping.Jitter,
                    Loss = raw.PacketLoss,
                    Download = raw.Download.Bandwidth / 125000.0,
                    Upload = raw.Upload.Bandwidth / 125000.0
                };
            }
            catch (Exception ex)
            {
                Logger.Write("Speedtest ERROR: " + ex.Message);
                return null;
            }
        }
    }

    public class SpeedtestRaw
    {
        [JsonPropertyName("ping")]
        public PingData Ping { get; set; } = new();

        [JsonPropertyName("download")]
        public BandwidthData Download { get; set; } = new();

        [JsonPropertyName("upload")]
        public BandwidthData Upload { get; set; } = new();

        [JsonPropertyName("packetLoss")]
        public double PacketLoss { get; set; }

        [JsonPropertyName("interface")]
        public InterfaceData Interface { get; set; } = new();

        [JsonPropertyName("server")]
        public ServerData Server { get; set; } = new();

        public class PingData
        {
            [JsonPropertyName("latency")]
            public double Latency { get; set; }

            [JsonPropertyName("jitter")]
            public double Jitter { get; set; }
        }

        public class BandwidthData
        {
            [JsonPropertyName("bandwidth")]
            public double Bandwidth { get; set; }
        }

        public class InterfaceData
        {
            [JsonPropertyName("externalIp")]
            public string? ExternalIp { get; set; }
        }

        public class ServerData
        {
            [JsonPropertyName("country")]
            public string? Country { get; set; }
        }
    }
}
