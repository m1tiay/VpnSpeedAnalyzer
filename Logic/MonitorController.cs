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

        public MonitorController(IIpInfoService ipService, ISpeedtestService speedtest)
        {
            _ipService = ipService ?? throw new ArgumentNullException(nameof(ipService));
            _speedtest = speedtest ?? throw new ArgumentNullException(nameof(speedtest));
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

            _cts.Cancel();
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
                        Logger.Write("Проверяем IP адрес...");
                        var info = await _ipService.GetCurrentAsync()
                            .ConfigureAwait(false);
                        Logger.Write("Ответ IP API: " + (info?.Ip ?? "NULL"));

                        if (info != null)
                        {
                            if (_lastIp == null || info.Ip != _lastIp.Ip)
                            {
                                Logger.Write("Ип-адрес изменился, запускаем тест скорости...");
                                var result = await _speedtest.RunAsync()
                                    .ConfigureAwait(false);
                                Logger.Write("Speedtest result: " + (result == null ? "NULL" : "OK"));

                                if (result != null)
                                    NewResult?.Invoke(this, result);
                            }
                            else
                            {
                                Logger.Write("Ип-адрес не изменился, пропускаем тест скорости");
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
                        // Continue looping even on error
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
