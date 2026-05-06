using System;
using System.Threading;
using System.Threading.Tasks;
using VpnSpeedAnalyzer.Models;
using VpnSpeedAnalyzer.Services;

namespace VpnSpeedAnalyzer.Logic
{
    /// <summary>
    /// Контроллер, который мониторит изменения IP адреса и запускает тесты скорости
    /// </summary>
    public class MonitorController : IDisposable
    {
        // Пауза между проверками IP адреса (15 секунд)
        private const int CheckIntervalMs = 15000;

        private readonly IIpInfoService _ipService;
        private readonly ISpeedtestService _speedtest;

        private CancellationTokenSource? _cts;
        private IpInfo? _lastIp;

        public event EventHandler<SpeedtestResult>? NewResult;
        public event EventHandler<IpInfo>? IpInfoUpdated;
        public event EventHandler<string>? StatusMessage;

        public MonitorController(IIpInfoService ipService, ISpeedtestService speedtest)
        {
            Logger.Write("MonitorController конструктор: проверка параметров");
            _ipService = ipService ?? throw new ArgumentNullException(nameof(ipService));
            Logger.Write("MonitorController: ipService ОК");
            _speedtest = speedtest ?? throw new ArgumentNullException(nameof(speedtest));
            Logger.Write("MonitorController: speedtest ОК");
        }

        /// <summary>
        /// Начинает мониторинг
        /// </summary>
        public void Start()
        {
            if (_cts != null)
            {
                Logger.Write("Монитор уже работает");
                return;
            }

            _cts = new CancellationTokenSource();
            _ = LoopAsync(_cts.Token);
        }

        /// <summary>
        /// Останавливает мониторинг
        /// </summary>
        public void Stop()
        {
            if (_cts == null)
            {
                Logger.Write("Монитор не работает");
                return;
            }

            var cts = _cts;
            _cts = null;

            cts.Cancel();
            cts.Dispose();
        }

        /// <summary>
        /// Очищает ресурсы
        /// </summary>
        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
            _cts = null;
        }

        /// <summary>
        /// Основной цикл мониторинга
        /// </summary>
        private async Task LoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        StatusMessage?.Invoke(this, "CHECKING: Идет проверка IP и состояния соединения...");
                        Logger.Write("Проверяем IP адрес...");
                        var info = await _ipService.GetCurrentAsync()
                            .ConfigureAwait(false);
                        Logger.Write("Ответ IP API: " + (info?.Ip ?? "NULL"));

                        if (info != null)
                        {
                            IpInfoUpdated?.Invoke(this, info);
                            var sourceName = string.IsNullOrWhiteSpace(_ipService.LastSourceName) ? "unknown" : _ipService.LastSourceName;
                            StatusMessage?.Invoke(this, $"INFO: Источник IP: {sourceName}");

                            if (_lastIp == null || info.Ip != _lastIp.Ip)
                            {
                                StatusMessage?.Invoke(this, $"CHECKING: IP обновлен через {sourceName}, запускаем speedtest...");
                                Logger.Write("Ип-адрес изменился, запускаем тест скорости...");
                                var result = await _speedtest.RunAsync()
                                    .ConfigureAwait(false);
                                Logger.Write("Speedtest result: " + (result == null ? "NULL" : "OK"));

                                if (result != null)
                                {
                                    result.Ip = info.Ip;
                                    result.Country = string.IsNullOrWhiteSpace(info.CountryName) ? result.Country : info.CountryName;
                                    result.CountryCode = info.CountryCode;
                                    result.Asn = info.Asn;
                                    NewResult?.Invoke(this, result);
                                }
                                else
                                {
                                    var reason = _speedtest.LastFailureReason ?? "Неизвестная ошибка speedtest";
                                    StatusMessage?.Invoke(this, $"ERROR: Ошибка замера: {reason}");
                                }
                            }
                            else
                            {
                                Logger.Write("Ип-адрес не изменился, пропускаем тест скорости");
                                StatusMessage?.Invoke(this, $"INFO: Идет проверка через {sourceName}, изменений IP не обнаружено");
                            }

                            _lastIp = info;
                        }

                        await Task.Delay(CheckIntervalMs, token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.Write("Monitor loop cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Write($"Monitor loop error: {ex.GetType().Name}: {ex.Message}");
                        StatusMessage?.Invoke(this, $"ERROR: Сбой цикла мониторинга: {ex.Message}");
                        // Продолжаем цикл даже при ошибке.
                        try
                        {
                            await Task.Delay(CheckIntervalMs, token)
                                .ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                Logger.Write("Monitor loop ended");
            }
        }
    }
}
