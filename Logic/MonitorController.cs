using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        // Стабилизация fingerprint transport-соединений, чтобы не реагировать на краткие колебания после speedtest.
        private const int VpnTransportDebounceMs = 2500;
        // Минимально значимое изменение transport fingerprint (симметричная разница endpoint'ов).
        private const int VpnTransportMinDeltaEndpoints = 4;

        // Принудительный замер скорости даже без изменения контура — раз в 5 минут.
        private static readonly TimeSpan ForcedRunInterval = TimeSpan.FromMinutes(5);
        // Защита от «дребезга» источников событий: не запускать авто-замеры слишком часто подряд.
        private static readonly TimeSpan MinAutomaticRunGap = TimeSpan.FromSeconds(5);

        private readonly IIpInfoService _ipService;
        private readonly ISpeedtestService _speedtest;

        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _delayCts;
        private Task? _loopTask;

        private string? _lastVpnTransportFingerprint;
        private bool _vpnDebounceChangeInProgress;
        private DateTime _vpnDebounceCandidateSince;
        private int _vpnTopProcessId;
        private string _vpnTopProcessLabel = "процесс: —";

        private DateTime _lastSpeedtestUtc = DateTime.MinValue;

        private bool _forceRunRequested;
        private bool _isMeasurementInFlight;

        public event EventHandler<SpeedtestResult>? NewResult;

        /// <summary>Обновление IP/гео после замера или при запросе (не каждый тик локального цикла).</summary>
        public event EventHandler<IpInfo>? IpInfoUpdated;
        public event EventHandler<string>? VpnProcessInfoUpdated;
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

            _lastVpnTransportFingerprint = null;
            _vpnDebounceChangeInProgress = false;
            _vpnTopProcessId = 0;
            _vpnTopProcessLabel = "процесс: —";
            _isMeasurementInFlight = false;
            VpnProcessInfoUpdated?.Invoke(this, _vpnTopProcessLabel);
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

            _lastVpnTransportFingerprint = null;
            _vpnDebounceChangeInProgress = false;
            _vpnTopProcessId = 0;
            _vpnTopProcessLabel = "процесс: —";
            _isMeasurementInFlight = false;
            VpnProcessInfoUpdated?.Invoke(this, _vpnTopProcessLabel);

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
        /// True, если transport fingerprint отличается от последнего принятого и стабилен не меньше <see cref="VpnTransportDebounceMs"/> мс.
        /// decisionLog возвращает диагностику принятого решения для подробного лога.
        /// </summary>
        private bool TryConfirmVpnTransportChange(string fingerprint, out string decisionLog)
        {
            decisionLog = string.Empty;

            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                decisionLog = "fingerprint пустой";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_lastVpnTransportFingerprint))
            {
                _lastVpnTransportFingerprint = fingerprint;
                _vpnDebounceChangeInProgress = false;
                decisionLog = $"инициализация базы: endpoints={CountFingerprintEndpoints(fingerprint)}";
                return false;
            }

            if (string.Equals(fingerprint, _lastVpnTransportFingerprint, StringComparison.Ordinal))
            {
                _vpnDebounceChangeInProgress = false;
                decisionLog = $"без изменений: endpoints={CountFingerprintEndpoints(fingerprint)}";
                return false;
            }

            var delta = CountEndpointDelta(_lastVpnTransportFingerprint, fingerprint);
            if (delta < VpnTransportMinDeltaEndpoints)
            {
                _vpnDebounceChangeInProgress = false;
                decisionLog =
                    $"изменение мало: delta={delta} < порог={VpnTransportMinDeltaEndpoints}, endpoints={CountFingerprintEndpoints(fingerprint)}";
                return false;
            }

            // Подтверждаем не «одинаковый снимок», а устойчивое состояние «delta >= порога».
            // Это важно для клиентов, где при реальной смене хоста endpoint'ы продолжают
            // достраиваться несколько секунд и fingerprint меняется на каждом тике.
            if (!_vpnDebounceChangeInProgress)
            {
                _vpnDebounceChangeInProgress = true;
                _vpnDebounceCandidateSince = DateTime.UtcNow;
                decisionLog =
                    $"значимое изменение: delta={delta}, старт debounce={VpnTransportDebounceMs}мс, endpoints={CountFingerprintEndpoints(fingerprint)}";
                return false;
            }

            var waitedMs = (DateTime.UtcNow - _vpnDebounceCandidateSince).TotalMilliseconds;
            if (waitedMs < VpnTransportDebounceMs)
            {
                decisionLog =
                    $"debounce: delta={delta}, ждём {waitedMs:F0}/{VpnTransportDebounceMs}мс, endpoints={CountFingerprintEndpoints(fingerprint)}";
                return false;
            }

            _lastVpnTransportFingerprint = fingerprint;
            _vpnDebounceChangeInProgress = false;
            decisionLog =
                $"смена подтверждена: delta={delta}, выдержка={waitedMs:F0}мс, endpoints={CountFingerprintEndpoints(fingerprint)}";
            return true;
        }

        private static int CountEndpointDelta(string previous, string current)
        {
            var prevSet = SplitFingerprint(previous);
            var curSet = SplitFingerprint(current);

            var removed = prevSet.Except(curSet, StringComparer.Ordinal).Count();
            var added = curSet.Except(prevSet, StringComparer.Ordinal).Count();
            return removed + added;
        }

        private static HashSet<string> SplitFingerprint(string value)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(value))
                return set;

            foreach (var s in value.Split('|', StringSplitOptions.RemoveEmptyEntries))
                set.Add(s.Trim());

            return set;
        }

        private static int CountFingerprintEndpoints(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            return value.Split('|', StringSplitOptions.RemoveEmptyEntries).Length;
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
                        var vpnSnapshot = VpnTransportFingerprint.GetTopProcessSnapshot(_vpnTopProcessId);
                        if (vpnSnapshot != null && vpnSnapshot.ProcessId != _vpnTopProcessId)
                        {
                            Logger.Write($"VPN transport: выбран PID {vpnSnapshot.ProcessId} (соединений: {vpnSnapshot.ConnectionCount})");
                            _vpnTopProcessId = vpnSnapshot.ProcessId;
                            UpdateVpnProcessLabel(vpnSnapshot.ProcessId);
                        }

                        var vpnDecision = "snapshot отсутствует";
                        var vpnTransportChanged = false;
                        if (vpnSnapshot != null)
                            vpnTransportChanged = TryConfirmVpnTransportChange(vpnSnapshot.Fingerprint, out vpnDecision);

                        if (vpnSnapshot == null)
                        {
                            Logger.Write("VPN transport tick: snapshot отсутствует (нет tunnel TCP ESTABLISHED)");
                        }
                        else
                        {
                            Logger.Write(
                                $"VPN transport tick: pid={vpnSnapshot.ProcessId}, conn={vpnSnapshot.ConnectionCount}, " +
                                $"endpoints={CountFingerprintEndpoints(vpnSnapshot.Fingerprint)}, changed={vpnTransportChanged}, {vpnDecision}");
                        }

                        var utcNow = DateTime.UtcNow;

                        var sinceLastMeasurement = utcNow - _lastSpeedtestUtc;
                        var intervalElapsed = sinceLastMeasurement >= ForcedRunInterval;
                        var firstMeasurement = _lastSpeedtestUtc == DateTime.MinValue;
                        var userRequested = _forceRunRequested;
                        var automaticTrigger = firstMeasurement || vpnTransportChanged || intervalElapsed;
                        var autoGapTooShort = !firstMeasurement
                                              && !intervalElapsed
                                              && sinceLastMeasurement < MinAutomaticRunGap;
                        var shouldMeasure = userRequested || (automaticTrigger && !autoGapTooShort);

                        Logger.Write(
                            $"Тик монитора: vpnTransportChanged={vpnTransportChanged}, " +
                            $"первыйЗамер={firstMeasurement}, прошло={sinceLastMeasurement.TotalSeconds:F0}s, " +
                            $"intervalElapsed={intervalElapsed}, force={userRequested}, antiBounce={autoGapTooShort}, inFlight={_isMeasurementInFlight}, run={shouldMeasure}");

                        if (shouldMeasure)
                        {
                            var reasonText = userRequested
                                ? "по запросу"
                                : vpnTransportChanged
                                    ? "смена VPN transport"
                                    : firstMeasurement
                                        ? "старт мониторинга"
                                        : "по таймеру";

                            StatusMessage?.Invoke(this, $"CHECK:Замер скорости ({reasonText})");
                            Logger.Write($"Запускаем тест скорости: {reasonText}");
                            _forceRunRequested = false;
                            _isMeasurementInFlight = true;

                            try
                            {
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
                            finally
                            {
                                _isMeasurementInFlight = false;
                                RebaselineHostChangeDetectors();
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
                                Logger.Write("Замер пропущен: VPN transport стабилен и таймер не истёк");
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

                        // Иначе нас просто разбудили через RequestImmediateRun — продолжаем цикл.
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

        /// <summary>
        /// После замера принимаем текущую сетевую картину как базовую, чтобы не ловить «хвост» внутренних перестроений.
        /// </summary>
        private void RebaselineHostChangeDetectors()
        {
            try
            {
                var vpnSnapshot = VpnTransportFingerprint.GetTopProcessSnapshot(_vpnTopProcessId);
                if (vpnSnapshot != null)
                {
                    _vpnTopProcessId = vpnSnapshot.ProcessId;
                    _lastVpnTransportFingerprint = string.IsNullOrWhiteSpace(vpnSnapshot.Fingerprint)
                        ? _lastVpnTransportFingerprint
                        : vpnSnapshot.Fingerprint;
                }
                _vpnDebounceChangeInProgress = false;
            }
            catch (Exception ex)
            {
                Logger.Write($"Rebaseline detectors: {ex.Message}");
            }
        }

        private void UpdateVpnProcessLabel(int processId)
        {
            var name = "unknown";
            try
            {
                name = Process.GetProcessById(processId).ProcessName;
            }
            catch
            {
                // Процесс мог завершиться между снимками — оставим unknown.
            }

            var next = $"{name} ({processId})";
            if (string.Equals(_vpnTopProcessLabel, next, StringComparison.Ordinal))
                return;

            _vpnTopProcessLabel = next;
            VpnProcessInfoUpdated?.Invoke(this, _vpnTopProcessLabel);
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
