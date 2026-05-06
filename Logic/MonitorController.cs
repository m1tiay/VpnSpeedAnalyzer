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
        // Пауза между проверками IP адреса (15 секунд).
        private const int CheckIntervalMs = 15000;

        // Принудительный замер скорости даже без смены IP — раз в 5 минут.
        private static readonly TimeSpan ForcedRunInterval = TimeSpan.FromMinutes(5);

        private readonly IIpInfoService _ipService;
        private readonly ISpeedtestService _speedtest;

        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _delayCts;
        private Task? _loopTask;
        private IpInfo? _lastIp;
        private DateTime _lastSpeedtestUtc = DateTime.MinValue;
        private bool _forceRunRequested;

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
            _loopTask = LoopAsync(_cts.Token);
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

            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            _delayCts?.Cancel();
            cts.Dispose();
            _loopTask = null;
        }

        /// <summary>
        /// Просит монитор как можно скорее выполнить новый замер скорости
        /// (даже если IP не изменился). Если монитор остановлен — ничего не делает.
        /// </summary>
        public void RequestImmediateRun()
        {
            if (_cts == null)
            {
                Logger.Write("RequestImmediateRun: монитор не запущен");
                return;
            }

            Logger.Write("Запрошен внеплановый замер");
            _forceRunRequested = true;
            try
            {
                _delayCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// Очищает ресурсы
        /// </summary>
        public void Dispose()
        {
            Stop();
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

                            var ipChanged = _lastIp == null || info.Ip != _lastIp.Ip;
                            var intervalElapsed = (DateTime.UtcNow - _lastSpeedtestUtc) >= ForcedRunInterval;
                            var shouldMeasure = ipChanged || intervalElapsed || _forceRunRequested;

                            if (shouldMeasure)
                            {
                                var reasonText = _forceRunRequested
                                    ? "по запросу пользователя"
                                    : ipChanged
                                        ? "обнаружена смена IP"
                                        : "по таймеру";

                                StatusMessage?.Invoke(this, $"CHECKING: Запускаем speedtest ({reasonText})...");
                                Logger.Write($"Запускаем тест скорости: {reasonText}");
                                _forceRunRequested = false;

                                var result = await _speedtest.RunAsync()
                                    .ConfigureAwait(false);
                                _lastSpeedtestUtc = DateTime.UtcNow;
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
                                Logger.Write("Замер скорости пропущен: IP не менялся и таймер не истёк");
                                StatusMessage?.Invoke(this, $"INFO: Идет проверка через {sourceName}, изменений IP не обнаружено");
                            }

                            _lastIp = info;
                        }

                        await DelayAsync(CheckIntervalMs, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested)
                        {
                            Logger.Write("Monitor loop cancelled");
                            break;
                        }
                        // Иначе нас просто разбудили через RequestImmediateRun — продолжаем цикл.
                    }
                    catch (Exception ex)
                    {
                        Logger.Write($"Monitor loop error: {ex.GetType().Name}: {ex.Message}");
                        StatusMessage?.Invoke(this, $"ERROR: Сбой цикла мониторинга: {ex.Message}");
                        try
                        {
                            await DelayAsync(CheckIntervalMs, token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested)
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

        private async Task DelayAsync(int milliseconds, CancellationToken token)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
            _delayCts = linked;
            try
            {
                await Task.Delay(milliseconds, linked.Token).ConfigureAwait(false);
            }
            finally
            {
                _delayCts = null;
            }
        }
    }
}
