using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace VpnSpeedAnalyzer
{
    public class IpInfoService
    {
        private static readonly HttpClient _http = new();

        public async Task<IpInfo> GetCurrentAsync()
        {
            try
            {
                var json = await _http.GetStringAsync("https://ipwho.is/");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // ipwho.is returns { "success": false } on error
                if (root.TryGetProperty("success", out var successProp) &&
                    successProp.ValueKind == JsonValueKind.False)
                {
                    return null;
                }

                var conn = root.GetProperty("connection");

                return new IpInfo
                {
                    Ip = root.GetProperty("ip").GetString(),
                    CountryCode = root.GetProperty("country_code").GetString(),
                    CountryName = root.GetProperty("country").GetString(),
                    Asn = conn.GetProperty("asn").ToString(),
                    Provider = conn.GetProperty("isp").GetString()
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
