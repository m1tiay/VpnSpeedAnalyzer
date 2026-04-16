using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using VpnSpeedAnalyzer.Models;

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
            catch
            {
                return null;
            }
        }
    }
}
