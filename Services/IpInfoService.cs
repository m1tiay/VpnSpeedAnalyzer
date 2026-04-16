using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using VpnSpeedAnalyzer.Models;
using VpnSpeedAnalyzer.Logic;

namespace VpnSpeedAnalyzer.Services
{
    public class IpInfoService
    {
        private readonly HttpClient _http = new();

        public async Task<IpInfo?> GetCurrentAsync()
        {
            try
            {
                var json = await _http.GetStringAsync("https://ipapi.co/json/");
                return JsonSerializer.Deserialize<IpInfo>(json);
            }
            catch (Exception ex)
            {
                Logger.Write("IP API ERROR: " + ex.Message);
                return null;
            }

        }
    }
}
