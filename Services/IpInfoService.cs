using System;
using System.Net.Http;
using System.Net.Http.Headers;
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
        private const string UserAgent = "VpnSpeedAnalyzer/1.0 (+https://github.com/m1tiay/VpnSpeedAnalyzer)";

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
                var json = await SendGetStringAsync(PrimaryIpApiUrl, "ipwho.is").ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                var dto = JsonSerializer.Deserialize<IpWhoIsResponse>(json);
                if (dto == null || !dto.Success || string.IsNullOrWhiteSpace(dto.Ip))
                {
                    Logger.Write("ipwho.is: ответ не содержит валидных полей success/ip");
                    return null;
                }

                return new IpInfo
                {
                    Ip = dto.Ip,
                    CountryName = dto.Country ?? string.Empty,
                    CountryCode = dto.CountryCode ?? string.Empty,
                    Asn = dto.Connection?.Asn ?? string.Empty
                };
            }
            catch (HttpRequestException ex)
            {
                Logger.Write($"ipwho.is HTTP ошибка: {ex.Message}");
                return null;
            }
            catch (TaskCanceledException ex)
            {
                Logger.Write($"ipwho.is таймаут: {ex.Message}");
                return null;
            }
            catch (JsonException ex)
            {
                Logger.Write($"ipwho.is ошибка JSON: {ex.Message}");
                return null;
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
                var json = await SendGetStringAsync(FallbackIpApiUrl, "ipapi.co").ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                var dto = JsonSerializer.Deserialize<IpApiCoResponse>(json);
                if (dto == null || string.IsNullOrWhiteSpace(dto.Ip))
                {
                    Logger.Write("ipapi.co: ответ не содержит валидного поля ip");
                    return null;
                }

                return new IpInfo
                {
                    Ip = dto.Ip,
                    CountryName = dto.CountryName ?? string.Empty,
                    CountryCode = dto.CountryCode ?? string.Empty,
                    Asn = dto.Asn ?? string.Empty
                };
            }
            catch (HttpRequestException ex)
            {
                Logger.Write($"ipapi.co HTTP ошибка: {ex.Message}");
                return null;
            }
            catch (TaskCanceledException ex)
            {
                Logger.Write($"ipapi.co таймаут: {ex.Message}");
                return null;
            }
            catch (JsonException ex)
            {
                Logger.Write($"ipapi.co ошибка JSON: {ex.Message}");
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<string?> SendGetStringAsync(string url, string sourceName)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var preview = body.Length > 300 ? body.Substring(0, 300) : body;
                Logger.Write($"{sourceName}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Тело: {preview}");
                return null;
            }

            return body;
        }

        private sealed class IpWhoIsResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("ip")]
            public string? Ip { get; set; }

            [JsonPropertyName("country")]
            public string? Country { get; set; }

            [JsonPropertyName("country_code")]
            public string? CountryCode { get; set; }

            [JsonPropertyName("connection")]
            public IpWhoIsConnection? Connection { get; set; }
        }

        private sealed class IpApiCoResponse
        {
            [JsonPropertyName("ip")]
            public string? Ip { get; set; }

            [JsonPropertyName("country_name")]
            public string? CountryName { get; set; }

            [JsonPropertyName("country_code")]
            public string? CountryCode { get; set; }

            [JsonPropertyName("asn")]
            public string? Asn { get; set; }
        }

        private sealed class IpWhoIsConnection
        {
            [JsonPropertyName("asn")]
            public string? Asn { get; set; }
        }
    }
}
