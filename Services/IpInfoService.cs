using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using VpnSpeedAnalyzer.Models;
using VpnSpeedAnalyzer.Logic;

namespace VpnSpeedAnalyzer.Services
{
    /// <summary>
    /// Сервис для получения информации об текущем IP адресе с сервиса ipapi.co
    /// </summary>
    public class IpInfoService : IIpInfoService
    {
        private const string IpApiUrl = "https://ipapi.co/json/";
        private const int HttpTimeoutSeconds = 10;

        // Статический HttpClient переиспользуется для всех запросов (предотвращение истощения сокетов)
        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds)
        };

        public IpInfoService()
        {
            Logger.Write("IpInfoService конструктор вызван");
        }

        public async Task<IpInfo?> GetCurrentAsync()
        {
            try
            {
                var json = await _http.GetStringAsync(IpApiUrl)
                    .ConfigureAwait(false);

                if (string.IsNullOrEmpty(json))
                {
                    Logger.Write("IP API returned empty response");
                    return null;
                }

                return JsonSerializer.Deserialize<IpInfo>(json);
            }
            catch (HttpRequestException ex)
            {
                Logger.Write($"Ошибка HTTP при запросе к IP API: {ex.Message}");
                return null;
            }
            catch (TaskCanceledException ex)
            {
                Logger.Write($"Истек таймаут при запросе к IP API: {ex.Message}");
                return null;
            }
            catch (JsonException ex)
            {
                Logger.Write($"Ошибка парсинга JSON в IP API: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Write($"Неожиданная ошибка IP API: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }
    }
}
