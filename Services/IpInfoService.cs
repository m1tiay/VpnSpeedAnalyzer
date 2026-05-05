using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VpnSpeedAnalyzer.Models;
using VpnSpeedAnalyzer.Logic;

namespace VpnSpeedAnalyzer.Services
{
    /// <summary>
    /// Сервис для получения информации о текущем IP адресе.
    /// Основной источник: ipwho.is, резервный: ipapi.co.
    /// </summary>
    public class IpInfoService : IIpInfoService
    {
        private const string PrimaryIpApiUrl = "https://ipwho.is/";
        private const string FallbackIpApiUrl = "https://ipapi.co/json/";
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
                var primaryResult = await TryGetFromIpWhoIsAsync().ConfigureAwait(false);
                if (primaryResult != null)
                {
                    Logger.Write("IP получен через основной канал ipwho.is");
                    return primaryResult;
                }

                Logger.Write("Основной канал ipwho.is не вернул результат, пробуем резервный ipapi.co");

                var fallbackResult = await TryGetFromIpApiAsync().ConfigureAwait(false);
                if (fallbackResult != null)
                {
                    Logger.Write("IP получен через резервный канал ipapi.co");
                    return fallbackResult;
                }

                Logger.Write("Не удалось получить IP ни через основной, ни через резервный канал");
                return null;
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

        private static async Task<IpInfo?> TryGetFromIpWhoIsAsync()
        {
            try
            {
                var json = await _http.GetStringAsync(PrimaryIpApiUrl).ConfigureAwait(false);
                if (string.IsNullOrEmpty(json))
                    return null;

                var dto = JsonSerializer.Deserialize<IpWhoIsResponse>(json);
                if (dto == null || !dto.Success || string.IsNullOrWhiteSpace(dto.Ip))
                    return null;

                return new IpInfo
                {
                    Ip = dto.Ip,
                    CountryName = dto.Country ?? string.Empty
                };
            }
            catch
            {
                return null;
            }
        }

        private static async Task<IpInfo?> TryGetFromIpApiAsync()
        {
            try
            {
                var json = await _http.GetStringAsync(FallbackIpApiUrl).ConfigureAwait(false);
                if (string.IsNullOrEmpty(json))
                    return null;

                var dto = JsonSerializer.Deserialize<IpApiCoResponse>(json);
                if (dto == null || string.IsNullOrWhiteSpace(dto.Ip))
                    return null;

                return new IpInfo
                {
                    Ip = dto.Ip,
                    CountryName = dto.CountryName ?? string.Empty
                };
            }
            catch
            {
                return null;
            }
        }

        private sealed class IpWhoIsResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("ip")]
            public string? Ip { get; set; }

            [JsonPropertyName("country")]
            public string? Country { get; set; }
        }

        private sealed class IpApiCoResponse
        {
            [JsonPropertyName("ip")]
            public string? Ip { get; set; }

            [JsonPropertyName("country_name")]
            public string? CountryName { get; set; }
        }
    }
}
