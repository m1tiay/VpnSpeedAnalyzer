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
        // После локального обнаружения смены хоста ждём 3 секунды стабилизации, затем делаем внешний запрос + замер.
        private static readonly TimeSpan HostChangeExternalDelay = TimeSpan.FromSeconds(3);

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
        private DateTime _hostChangeObservedUtc = DateTime.MinValue;

        private DateTime _lastSpeedtestUtc = DateTime.MinValue;

        private bool _forceRunRequested;

        public event EventHandler<SpeedtestResult>? NewResult;

        /// <summary>Обновление IP/гео только в моменты запроса к внешнему сервису.</summary>
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
            _hostChangeObservedUtc = DateTime.MinValue;
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

            _lastVpnTransportFingerprint = null;
            _hostChangeObservedUtc = DateTime.MinValue;

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
                        var hostChangeSignal = topologyConfirmed || vpnTransportChanged;
                        if (hostChangeSignal)
                        {
                            // Если за 3 секунды пришла новая смена, таймер стабилизации начинается заново.
                            _hostChangeObservedUtc = utcNow;
                            Logger.Write("Зафиксирована локальная смена хоста: ждём 3 сек перед внешним запросом и замером");
                        }

                        var sinceLastMeasurement = utcNow - _lastSpeedtestUtc;
                        var intervalElapsed = sinceLastMeasurement >= ForcedRunInterval;
                        var firstMeasurement = _lastSpeedtestUtc == DateTime.MinValue;
                        var userRequested = _forceRunRequested;
                        var hostChangeReady = _hostChangeObservedUtc != DateTime.MinValue
                                              && (utcNow - _hostChangeObservedUtc) >= HostChangeExternalDelay;
                        var shouldMeasure = userRequested || firstMeasurement || hostChangeReady || intervalElapsed;

                        Logger.Write(
                            $"Тик монитора: topologyConfirmed={topologyConfirmed}, vpnTransportChanged={vpnTransportChanged}, hostChangeReady={hostChangeReady}, " +
                            $"первыйЗамер={firstMeasurement}, прошло={sinceLastMeasurement.TotalSeconds:F0}s, " +
                            $"intervalElapsed={intervalElapsed}, force={userRequested}, run={shouldMeasure}");

                        if (shouldMeasure)
                        {
                            IpInfo? geo = null;
                            var hostChangePlanned = hostChangeReady;
                            _hostChangeObservedUtc = DateTime.MinValue;

                            var reasonText = userRequested
                                ? "по запросу"
                                : hostChangePlanned
                                    ? "смена хоста"
                                        : firstMeasurement
                                            ? "старт мониторинга"
                                            : "по таймеру";

                            StatusMessage?.Invoke(this, $"CHECK:Замер скорости ({reasonText})");
                            Logger.Write($"Запускаем тест скорости: {reasonText}");
                            _forceRunRequested = false;
                            if (hostChangePlanned)
                            {
                                try
                                {
                                    geo = await _ipService.GetCurrentAsync().ConfigureAwait(false);
                                    if (geo != null)
                                        IpInfoUpdated?.Invoke(this, geo);
                                }
                                catch (Exception ex)
                                {
                                    Logger.Write($"Внешний запрос при смене хоста: {ex.Message}");
                                }
                            }

                            var speedProgress = new Progress<double>(p => SpeedtestProgress?.Invoke(this, p));
                            var result = await _speedtest.RunAsync(speedProgress, token).ConfigureAwait(false);
                            _lastSpeedtestUtc = DateTime.UtcNow;

                            Logger.Write("Speedtest result: " + (result == null ? "NULL" : "OK"));

                            if (result != null)
                            {
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
                            if (_hostChangeObservedUtc != DateTime.MinValue)
                            {
                                var waitLeft = HostChangeExternalDelay - (utcNow - _hostChangeObservedUtc);
                                if (waitLeft < TimeSpan.Zero)
                                    waitLeft = TimeSpan.Zero;
                                Logger.Write(
                                    $"Замер отложен: ждём стабилизацию смены хоста ещё {waitLeft.TotalMilliseconds:F0} мс");
                            }
                            else
                            {
                                Logger.Write("Замер пропущен: изменений хоста нет и таймер не истёк");
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
