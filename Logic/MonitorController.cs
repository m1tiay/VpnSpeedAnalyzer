using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using VpnSpeedAnalyzer.Models;
using VpnSpeedAnalyzer.Services;

namespace VpnSpeedAnalyzer.Logic
{
    /// <summary>
    /// Контроллер, который мониторит изменения сетевого контура и запускает тесты скорости
    /// </summary>
    public class MonitorController : IDisposable
    {
        // Локальный опрос конфигурации интерфейсов (без внешних HTTP) каждые 400 мс —
        // реакция на смену VPN/Wi‑Fi быстрее, чем периодический опрос только IP через API.
        private const int LocalPollIntervalMs = 400;

        // Сколько миллисекунд новый отпечаток должен оставаться неизменным, чтобы не ловить кратковременные «дёргания» DHCP/интерфейсов.
        private const int TopologyDebounceMs = 1200;

        // Принудительный замер скорости даже без изменения контура — раз в 5 минут.
        private static readonly TimeSpan ForcedRunInterval = TimeSpan.FromMinutes(5);
        // Защита от «дребезга» источников событий: не запускать авто-замеры слишком часто подряд.
        private static readonly TimeSpan MinAutomaticRunGap = TimeSpan.FromSeconds(5);

        // Резервное обнаружение смены VPN: публичный IP опрашивается реже, если локальный отпечаток не меняется.
        private const int EgressPollIntervalMs = 20000;

        private readonly IIpInfoService _ipService;
        private readonly ISpeedtestService _speedtest;

        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _delayCts;
        private Task? _loopTask;

        /// <summary>Принятый «стабильный» снимок контура после старта или последней подтверждённой смены.</summary>
        private string? _acceptedFingerprint;

        private string? _debounceCandidateFingerprint;
        private DateTime _debounceCandidateSince;
        private string? _lastVpnTransportFingerprint;

        private DateTime _lastSpeedtestUtc = DateTime.MinValue;

        private bool _forceRunRequested;

        /// <summary>Последний известный egress IP (гео-API), чтобы ловить смену выхода без смены локального отпечатка.</summary>
        private string? _lastSeenEgressIp;

        private DateTime _lastEgressPollUtc = DateTime.MinValue;

        public event EventHandler<SpeedtestResult>? NewResult;

        /// <summary>Обновление IP/гео после замера или при запросе (не каждый тик локального цикла).</summary>
        public event EventHandler<IpInfo>? IpInfoUpdated;
        public event EventHandler<string>? StatusMessage;

        /// <summary>Проценты 0..100 хода speedtest (stderr/фазовая оценка).</summary>
        public event EventHandler<double>? SpeedtestProgress;

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

            _acceptedFingerprint = null;
            _debounceCandidateFingerprint = null;
            _lastVpnTransportFingerprint = null;
            _lastSeenEgressIp = null;
            _lastEgressPollUtc = DateTime.MinValue;
            NetworkChange.NetworkAddressChanged += OnNetworkTopologyHint;
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

            NetworkChange.NetworkAddressChanged -= OnNetworkTopologyHint;

            _lastSeenEgressIp = null;
            _lastEgressPollUtc = DateTime.MinValue;
            _lastVpnTransportFingerprint = null;

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
        /// Событие ОС об изменении адресов: ускоряем следующее сравнение отпечатка.
        /// </summary>
        private void OnNetworkTopologyHint(object? sender, EventArgs e)
        {
            try
            {
                _delayCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// True, если отпечаток отличается от принятого и не менялся не меньше <see cref="TopologyDebounceMs"/> мс.
        /// </summary>
        private bool TryConfirmTopologyChange(string fingerprint)
        {
            if (_acceptedFingerprint == null)
                return false;

            if (string.Equals(fingerprint, _acceptedFingerprint, StringComparison.Ordinal))
            {
                _debounceCandidateFingerprint = null;
                return false;
            }

            if (!string.Equals(fingerprint, _debounceCandidateFingerprint, StringComparison.Ordinal))
            {
                _debounceCandidateFingerprint = fingerprint;
                _debounceCandidateSince = DateTime.UtcNow;
                Logger.Write("Сеть: новый отпечаток контура — ждём стабилизацию перед замером");
                return false;
            }

            var waitedMs = (DateTime.UtcNow - _debounceCandidateSince).TotalMilliseconds;
            if (waitedMs < TopologyDebounceMs)
                return false;

            Logger.Write($"Сеть: смена контура подтверждена после {waitedMs:F0} мс стабильного отпечатка");
            _acceptedFingerprint = fingerprint;
            _debounceCandidateFingerprint = null;
            return true;
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
                        var fingerprint = LocalNetworkFingerprint.Compute();

                        var topologyConfirmed = TryConfirmTopologyChange(fingerprint);
                        var vpnTransportChanged = false;
                        var vpnTransportFp = VpnTransportFingerprint.Compute();
                        if (!string.IsNullOrEmpty(vpnTransportFp))
                        {
                            if (!string.IsNullOrEmpty(_lastVpnTransportFingerprint)
                                && !string.Equals(_lastVpnTransportFingerprint, vpnTransportFp, StringComparison.Ordinal))
                            {
                                vpnTransportChanged = true;
                                Logger.Write("Обнаружена смена VPN transport fingerprint (remote IP:port на туннеле)");
                            }

                            _lastVpnTransportFingerprint = vpnTransportFp;
                        }

                        // Первый тик ещё не знает базовый контур — фиксируем текущий снимок без ожидания debounce.
                        _acceptedFingerprint ??= fingerprint;

                        var utcNow = DateTime.UtcNow;
                        var egressIpChanged = false;
                        if ((utcNow - _lastEgressPollUtc).TotalMilliseconds >= EgressPollIntervalMs
                            || _lastEgressPollUtc == DateTime.MinValue)
                        {
                            _lastEgressPollUtc = utcNow;
                            try
                            {
                                var ipSnap = await _ipService.GetCurrentAsync().ConfigureAwait(false);
                                if (ipSnap?.Ip is { Length: > 0 } ipCur)
                                {
                                    if (_lastSeenEgressIp != null
                                        && !_lastSeenEgressIp.Equals(ipCur, StringComparison.OrdinalIgnoreCase))
                                    {
                                        egressIpChanged = true;
                                        Logger.Write($"Смена egress IP (опрос {EgressPollIntervalMs}мс): {_lastSeenEgressIp} → {ipCur}");
                                        IpInfoUpdated?.Invoke(this, ipSnap);
                                    }
                                    else if (_lastSeenEgressIp == null)
                                    {
                                        IpInfoUpdated?.Invoke(this, ipSnap);
                                    }

                                    _lastSeenEgressIp = ipCur;
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Write($"Опрос egress IP: {ex.Message}");
                            }
                        }

                        var sinceLastMeasurement = utcNow - _lastSpeedtestUtc;
                        var intervalElapsed = sinceLastMeasurement >= ForcedRunInterval;
                        var firstMeasurement = _lastSpeedtestUtc == DateTime.MinValue;
                        var userRequested = _forceRunRequested;
                        var automaticTrigger = firstMeasurement || topologyConfirmed || egressIpChanged || vpnTransportChanged || intervalElapsed;
                        var autoGapTooShort = !firstMeasurement
                                              && !intervalElapsed
                                              && sinceLastMeasurement < MinAutomaticRunGap;
                        var shouldMeasure = userRequested || (automaticTrigger && !autoGapTooShort);

                        Logger.Write(
                            $"Тик монитора: topologyConfirmed={topologyConfirmed}, egressIpChanged={egressIpChanged}, vpnTransportChanged={vpnTransportChanged}, " +
                            $"первыйЗамер={firstMeasurement}, прошло={sinceLastMeasurement.TotalSeconds:F0}s, " +
                            $"intervalElapsed={intervalElapsed}, force={userRequested}, antiBounce={autoGapTooShort}, run={shouldMeasure}");

                        if (shouldMeasure)
                        {
                            var reasonText = userRequested
                                ? "по запросу"
                                : topologyConfirmed
                                    ? "смена сетевого контура"
                                    : egressIpChanged
                                        ? "смена публичного IP"
                                        : vpnTransportChanged
                                            ? "смена VPN transport"
                                        : firstMeasurement
                                            ? "старт мониторинга"
                                            : "по таймеру";

                            StatusMessage?.Invoke(this, $"CHECK:Замер скорости ({reasonText})");
                            Logger.Write($"Запускаем тест скорости: {reasonText}");
                            _forceRunRequested = false;

                            var speedProgress = new Progress<double>(p => SpeedtestProgress?.Invoke(this, p));
                            var result = await _speedtest.RunAsync(speedProgress, token).ConfigureAwait(false);
                            _lastSpeedtestUtc = DateTime.UtcNow;

                            Logger.Write("Speedtest result: " + (result == null ? "NULL" : "OK"));

                            if (result != null)
                            {
                                // Обогащаем результат геоданными (один HTTP на замер — с текущим выходом в интернет после VPN).
                                var geo = await _ipService.GetCurrentAsync().ConfigureAwait(false);

                                MergeGeoIntoResult(result, geo);

                                var sourceName = string.IsNullOrWhiteSpace(_ipService.LastSourceName)
                                    ? "unknown"
                                    : _ipService.LastSourceName;

                                if (geo != null)
                                {
                                    Logger.Write($"Геоданные к замеру: источник={sourceName}, IP(API)={geo.Ip}");
                                    IpInfoUpdated?.Invoke(this, geo);
                                }
                                else if (!string.IsNullOrWhiteSpace(result.Ip))
                                {
                                    IpInfoUpdated?.Invoke(
                                        this,
                                        new IpInfo
                                        {
                                            Ip = result.Ip,
                                            CountryName = string.Empty,
                                            CountryCode = result.CountryCode,
                                            Asn = result.Asn
                                        });

                                    Logger.Write("Гео API недоступен — в UI передан только IP из speedtest");
                                }

                                NewResult?.Invoke(this, result);
                            }
                            else
                            {
                                var reason = _speedtest.LastFailureReason ?? "Неизвестная ошибка speedtest";
                                StatusMessage?.Invoke(this, $"ERROR:Ошибка замера: {reason}");
                            }
                        }
                        else
                        {
                            if (autoGapTooShort)
                            {
                                Logger.Write(
                                    $"Замер пропущен: анти-дребезг автозапуска (прошло {sinceLastMeasurement.TotalSeconds:F0}с, минимум {MinAutomaticRunGap.TotalSeconds:F0}с)");
                            }
                            else
                            {
                                Logger.Write("Замер пропущен: контур стабилен и таймер не истёк");
                            }
                        }

                        await DelayAsync(LocalPollIntervalMs, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested)
                        {
                            Logger.Write("Monitor loop cancelled");
                            break;
                        }

                        // Иначе нас просто разбудили через RequestImmediateRun или NetworkAddressChanged — продолжаем цикл.
                    }
                    catch (Exception ex)
                    {
                        Logger.Write($"Monitor loop error: {ex.GetType().Name}: {ex.Message}");
                        StatusMessage?.Invoke(this, $"ERROR:Сбой мониторинга: {ex.Message}");
                        try
                        {
                            await DelayAsync(LocalPollIntervalMs, token).ConfigureAwait(false);
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

        /// <summary>
        /// Публичный IP из speedtest; страна и ASN из геосервиса (если ответ есть).
        /// </summary>
        private static void MergeGeoIntoResult(SpeedtestResult result, IpInfo? geo)
        {
            if (geo == null)
                return;

            if (!string.IsNullOrWhiteSpace(geo.Ip)
                && !string.Equals(geo.Ip, result.Ip, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Write($"Несовпадение egress IP: speedtest={result.Ip}, геосервис={geo.Ip} (в таблице оставляем speedtest)");
            }

            if (!string.IsNullOrWhiteSpace(geo.CountryName))
                result.Country = geo.CountryName;

            result.CountryCode = string.IsNullOrWhiteSpace(geo.CountryCode) ? result.CountryCode : geo.CountryCode;

            if (!string.IsNullOrWhiteSpace(geo.Asn))
                result.Asn = geo.Asn;
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
