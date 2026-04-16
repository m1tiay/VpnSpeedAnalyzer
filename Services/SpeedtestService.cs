using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

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

                var result = JsonSerializer.Deserialize<SpeedtestResult>(output);
                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}
