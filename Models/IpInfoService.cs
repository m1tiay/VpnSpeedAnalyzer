using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace VpnSpeedAnalyzer
{
    public class IpInfoService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        public async Task<IpInfo> GetCurrentAsync()
        {
            try
            {
                var json = await _http.GetStringAsync("https://check-host.net/ip-info");

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                return new IpInfo
                {
                    Ip = root.GetProperty("ip").GetString(),
                    CountryCode = root.GetProperty("country_code").GetString(),
                    CountryName = root.GetProperty("country").GetString(),
                    Asn = root.GetProperty("asn").GetString(),
                    Provider = root.GetProperty("provider").GetString()
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
