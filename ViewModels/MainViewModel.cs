using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using VpnSpeedAnalyzer.Logic;
using VpnSpeedAnalyzer.Models;
using VpnSpeedAnalyzer.Services;
using System.Collections.Generic;

namespace VpnSpeedAnalyzer
{
    /// <summary>
    /// Основная Виев-Модель приложения
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private const int TopHostsCount = 3;

        private readonly MonitorController _monitor;
        private readonly ResultsManager _resultsManager;
        private readonly ScoringService _scoring = new();

        private string _currentIp = "";
        private string _currentCountry = "";
        private string _currentAsn = "";
        // Единая палитра статуса, повторяет легенду в UI.
        private const string StatusColorOk = "#59D9B7";
        private const string StatusColorCheck = "#F6C453";
        private const string StatusColorWait = "#A8B0D9";
        private const string StatusColorError = "#FF7AA2";

        // Полный период до следующего автоматического замера.
        private static readonly TimeSpan ProgressFullPeriod = TimeSpan.FromMinutes(5);

        private string _statusText = "Остановлено";
        private string _statusColor = StatusColorWait;

        // Прогресс: заполняющая шкала между автозамерами (после первого успешного результата).
        private readonly DispatcherTimer _progressTimer;
        private DateTime _lastSuccessUtc;
        private bool _hadSuccessfulMeasurement;
        private bool _progressVisible;
        private bool _progressIsIndeterminate;
        private double _progressValue;
        private string _progressColor = StatusColorWait;
        private string _selectedScoringProfile = ScoringService.ProfileUniversal;
        private string _recommendationText = "Рекомендация появится после первого успешного замера";
        private string _ratingSummaryText = "Недостаточно данных для аналитики";
        private string _stabilitySummaryText = "Стабильность будет рассчитана после нескольких замеров";
        private string _bestHostSummaryText = "Лучший хост появится после первого успешного замера";
        private string _vpnProcessInfoText = "процесс: —";

        private HostRatingRow? _selectedHostRatingRow;

        private static readonly SolidColorBrush CmpBrushGood = CreateCmpBrushStatic("#59D9B7");
        private static readonly SolidColorBrush CmpBrushBad = CreateCmpBrushStatic("#FF7AA2");
        private static readonly SolidColorBrush CmpBrushNeutral = CreateCmpBrushStatic("#A8B0D9");

        private string _cmpDqsLeaderText = "";
        private string _cmpDqsDeltaText = "";
        private Brush _cmpDqsDeltaBrush = CmpBrushNeutral;

        private string _cmpPingLeaderText = "";
        private string _cmpPingDeltaText = "";
        private Brush _cmpPingDeltaBrush = CmpBrushNeutral;

        private string _cmpJitterLeaderText = "";
        private string _cmpJitterDeltaText = "";
        private Brush _cmpJitterDeltaBrush = CmpBrushNeutral;

        private string _cmpLossLeaderText = "";
        private string _cmpLossDeltaText = "";
        private Brush _cmpLossDeltaBrush = CmpBrushNeutral;

        private string _cmpDownLeaderText = "";
        private string _cmpDownDeltaText = "";
        private Brush _cmpDownDeltaBrush = CmpBrushNeutral;

        private string _cmpUpLeaderText = "";
        private string _cmpUpDeltaText = "";
        private Brush _cmpUpDeltaBrush = CmpBrushNeutral;

        private ResultEntry? _selectedResult;
        private bool _isMonitoring;
        private int _selectedMainTabIndex;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<SpeedtestResult>? NewResultArrived;

        public ObservableCollection<ResultEntry> Results { get; } = new();
        public ObservableCollection<ResultEntry> TopHosts { get; } = new();
        public ObservableCollection<HostRatingRow> HostRatings { get; } = new();
        public IReadOnlyList<string> ScoringProfiles => _scoring.AvailableProfiles;

        public double ScoringProfileComboWidth
        {
            get
            {
                var maxTextWidth = 0.0;
                var pixelsPerDip = 1.0;
                if (Application.Current?.MainWindow != null)
                    pixelsPerDip = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip;

                foreach (var profile in ScoringProfiles)
                {
                    if (string.IsNullOrWhiteSpace(profile))
                        continue;

                    var formattedText = new FormattedText(
                        profile,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        12,
                        Brushes.Transparent,
                        pixelsPerDip);

                    if (formattedText.Width > maxTextWidth)
                        maxTextWidth = formattedText.Width;
                }

                // Текст + симметричные внутренние отступы + небольшой запас под рамку.
                var calculatedWidth = maxTextWidth + 22;
                return Math.Max(120, Math.Ceiling(calculatedWidth));
            }
        }

        public string CurrentIp
        {
            get => _currentIp;
            set
            {
                if (_currentIp != value)
                {
                    _currentIp = value;
                    NotifyPropertyChanged(nameof(CurrentIp));
                }
            }
        }

        /// <summary>
        /// Текущая страна
        /// </summary>
        public string CurrentCountry
        {
            get => _currentCountry;
            set
            {
                if (_currentCountry != value)
                {
                    _currentCountry = value;
                    NotifyPropertyChanged(nameof(CurrentCountry));
                }
            }
        }

        /// <summary>
        /// Автономные системы (автономные системы)
        /// </summary>
        public string CurrentAsn
        {
            get => _currentAsn;
            set
            {
                if (_currentAsn != value)
                {
                    _currentAsn = value;
                    NotifyPropertyChanged(nameof(CurrentAsn));
                }
            }
        }

        /// <summary>
        /// Текст статуса
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    NotifyPropertyChanged(nameof(StatusText));
                }
            }
        }

        /// <summary>
        /// Цвет текста статуса в формате HEX (поддерживает стиль палитры интерфейса)
        /// </summary>
        public string StatusColor
        {
            get => _statusColor;
            set
            {
                if (_statusColor != value)
                {
                    _statusColor = value;
                    NotifyPropertyChanged(nameof(StatusColor));
                }
            }
        }

        /// <summary>
        /// Активный профиль оценки качества хоста
        /// </summary>
        public string SelectedScoringProfile
        {
            get => _selectedScoringProfile;
            set
            {
                if (_selectedScoringProfile != value)
                {
                    _selectedScoringProfile = value;
                    _scoring.ActiveProfile = value;
                    RecalculateScores();
                    NotifyPropertyChanged(nameof(SelectedScoringProfile));
                    NotifyPropertyChanged(nameof(SelectedScoringProfileDescription));
                }
            }
        }

        /// <summary>
        /// Описание активного профиля D.Q.S для подсказок в UI.
        /// </summary>
        public string SelectedScoringProfileDescription => _scoring.Describe();

        /// <summary>
        /// Краткая рекомендация по лучшему доступному хосту
        /// </summary>
        public string RecommendationText
        {
            get => _recommendationText;
            set
            {
                if (_recommendationText != value)
                {
                    _recommendationText = value;
                    NotifyPropertyChanged(nameof(RecommendationText));
                }
            }
        }

        /// <summary>
        /// Краткая сводка по распределению рейтинга.
        /// </summary>
        public string RatingSummaryText
        {
            get => _ratingSummaryText;
            set
            {
                if (_ratingSummaryText != value)
                {
                    _ratingSummaryText = value;
                    NotifyPropertyChanged(nameof(RatingSummaryText));
                }
            }
        }

        /// <summary>
        /// Текстовая оценка стабильности по джиттеру и потерям.
        /// </summary>
        public string StabilitySummaryText
        {
            get => _stabilitySummaryText;
            set
            {
                if (_stabilitySummaryText != value)
                {
                    _stabilitySummaryText = value;
                    NotifyPropertyChanged(nameof(StabilitySummaryText));
                }
            }
        }

        /// <summary>
        /// Краткая аналитика по лучшему хосту.
        /// </summary>
        public string BestHostSummaryText
        {
            get => _bestHostSummaryText;
            set
            {
                if (_bestHostSummaryText != value)
                {
                    _bestHostSummaryText = value;
                    NotifyPropertyChanged(nameof(BestHostSummaryText));
                }
            }
        }

        public string VpnProcessInfoText
        {
            get => _vpnProcessInfoText;
            set
            {
                if (_vpnProcessInfoText != value)
                {
                    _vpnProcessInfoText = value;
                    NotifyPropertyChanged(nameof(VpnProcessInfoText));
                }
            }
        }

        public string TotalMeasurements => Results.Count == 0 ? string.Empty : Results.Count.ToString(CultureInfo.InvariantCulture);
        public string UniqueHostsCount => HostRatings.Count == 0 ? string.Empty : HostRatings.Count.ToString(CultureInfo.InvariantCulture);

        /// <summary>Выбранная строка таблицы аналитики (сравнение с лидером).</summary>
        public HostRatingRow? SelectedHostRatingRow
        {
            get => _selectedHostRatingRow;
            set
            {
                if (!ReferenceEquals(_selectedHostRatingRow, value))
                {
                    _selectedHostRatingRow = value;
                    NotifyPropertyChanged(nameof(SelectedHostRatingRow));
                    RefreshLeaderComparison();
                }
            }
        }

        public string CmpDqsLeaderText => _cmpDqsLeaderText;
        public string CmpDqsDeltaText => _cmpDqsDeltaText;
        public string CmpDqsSeparatorText => BuildComparisonSeparator(_cmpDqsLeaderText, _cmpDqsDeltaText);
        public Brush CmpDqsDeltaBrush => _cmpDqsDeltaBrush;

        public string CmpPingLeaderText => _cmpPingLeaderText;
        public string CmpPingDeltaText => _cmpPingDeltaText;
        public string CmpPingSeparatorText => BuildComparisonSeparator(_cmpPingLeaderText, _cmpPingDeltaText);
        public Brush CmpPingDeltaBrush => _cmpPingDeltaBrush;

        public string CmpJitterLeaderText => _cmpJitterLeaderText;
        public string CmpJitterDeltaText => _cmpJitterDeltaText;
        public string CmpJitterSeparatorText => BuildComparisonSeparator(_cmpJitterLeaderText, _cmpJitterDeltaText);
        public Brush CmpJitterDeltaBrush => _cmpJitterDeltaBrush;

        public string CmpLossLeaderText => _cmpLossLeaderText;
        public string CmpLossDeltaText => _cmpLossDeltaText;
        public string CmpLossSeparatorText => BuildComparisonSeparator(_cmpLossLeaderText, _cmpLossDeltaText);
        public Brush CmpLossDeltaBrush => _cmpLossDeltaBrush;

        public string CmpDownLeaderText => _cmpDownLeaderText;
        public string CmpDownDeltaText => _cmpDownDeltaText;
        public string CmpDownSeparatorText => BuildComparisonSeparator(_cmpDownLeaderText, _cmpDownDeltaText);
        public Brush CmpDownDeltaBrush => _cmpDownDeltaBrush;

        public string CmpUpLeaderText => _cmpUpLeaderText;
        public string CmpUpDeltaText => _cmpUpDeltaText;
        public string CmpUpSeparatorText => BuildComparisonSeparator(_cmpUpLeaderText, _cmpUpDeltaText);
        public Brush CmpUpDeltaBrush => _cmpUpDeltaBrush;

        /// <summary>
        /// Видим ли индикатор прогресса в верхней панели.
        /// </summary>
        public bool ProgressVisible
        {
            get => _progressVisible;
            private set
            {
                if (_progressVisible != value)
                {
                    _progressVisible = value;
                    NotifyPropertyChanged(nameof(ProgressVisible));
                }
            }
        }

        /// <summary>
        /// Если true — индикатор работает в режиме «бегущего блика» (идёт замер).
        /// </summary>
        public bool ProgressIsIndeterminate
        {
            get => _progressIsIndeterminate;
            private set
            {
                if (_progressIsIndeterminate != value)
                {
                    _progressIsIndeterminate = value;
                    NotifyPropertyChanged(nameof(ProgressIsIndeterminate));
                }
            }
        }

        /// <summary>
        /// Заполненность шкалы 0..100 при отсчёте до следующего автозамера (или во время замера по данным speedtest).
        /// </summary>
        public double ProgressValue => _progressValue;

        /// <summary>
        /// Цвет индикатора, повторяет цвет статуса.
        /// </summary>
        public string ProgressColor
        {
            get => _progressColor;
            private set
            {
                if (_progressColor != value)
                {
                    _progressColor = value;
                    NotifyPropertyChanged(nameof(ProgressColor));
                }
            }
        }

        /// <summary>
        /// Выбранная запись в таблице результатов
        /// </summary>
        public ResultEntry? SelectedResult
        {
            get => _selectedResult;
            set
            {
                if (_selectedResult != value)
                {
                    _selectedResult = value;
                    NotifyPropertyChanged(nameof(SelectedResult));
                }
            }
        }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ToggleMonitoringCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ExportTopHostsCsvCommand { get; }
        public ICommand ShowMonitoringTabCommand { get; }
        public ICommand ShowAnalyticsTabCommand { get; }
        public RelayCommand RunNowCommand { get; }

        public string ToggleMonitoringButtonText => _isMonitoring ? "Стоп" : "Старт";
        public int SelectedMainTabIndex
        {
            get => _selectedMainTabIndex;
            set
            {
                if (_selectedMainTabIndex != value)
                {
                    _selectedMainTabIndex = value;
                    NotifyPropertyChanged(nameof(SelectedMainTabIndex));
                    NotifyPropertyChanged(nameof(IsMonitoringTabSelected));
                    NotifyPropertyChanged(nameof(IsAnalyticsTabSelected));
                    NotifyPropertyChanged(nameof(ActiveExportButtonText));
                    NotifyPropertyChanged(nameof(ActiveExportCommand));
                }
            }
        }

        public bool IsMonitoringTabSelected => SelectedMainTabIndex == 0;
        public bool IsAnalyticsTabSelected => SelectedMainTabIndex == 1;
        public string ActiveExportButtonText => IsAnalyticsTabSelected ? "Экспорт топа CSV" : "Экспорт CSV";
        public ICommand ActiveExportCommand => IsAnalyticsTabSelected ? ExportTopHostsCsvCommand : ExportCsvCommand;

        public MainViewModel()
        {
            try
            {
                Logger.Write("ViewModel: IpInfoService создание");
                // Создаём сервисы
                var ipService = new IpInfoService();
                Logger.Write("ViewModel: IpInfoService ОК");

                Logger.Write("ViewModel: SpeedtestService создание");
                var speedtestService = new SpeedtestService();
                Logger.Write("ViewModel: SpeedtestService ОК");

                Logger.Write("ViewModel: MonitorController создание");
                // Создаём контроллер монитора с иньекцией
                _monitor = new MonitorController(ipService, speedtestService);
                Logger.Write("ViewModel: MonitorController ОК");
                
                Logger.Write("ViewModel: Monitor.NewResult подписка");
                _monitor.NewResult += Monitor_NewResult;
                Logger.Write("ViewModel: Monitor.NewResult подписана");
                _monitor.IpInfoUpdated += Monitor_IpInfoUpdated;
                Logger.Write("ViewModel: Monitor.IpInfoUpdated подписана");
                _monitor.VpnProcessInfoUpdated += Monitor_VpnProcessInfoUpdated;
                Logger.Write("ViewModel: Monitor.VpnProcessInfoUpdated подписана");
                _monitor.StatusMessage += Monitor_StatusMessage;
                Logger.Write("ViewModel: Monitor.StatusMessage подписана");
                _monitor.SpeedtestProgress += Monitor_SpeedtestProgress;
                Logger.Write("ViewModel: Monitor.SpeedtestProgress подписана");

                Logger.Write("ViewModel: ResultsManager создание");
                // Создаём менеджер результатов
                _resultsManager = new ResultsManager(Results);
                Logger.Write("ViewModel: ResultsManager ОК");

                Logger.Write("ViewModel: Команды создание");
                // Создаём команды
                StartCommand = new RelayCommand(_ => Start());
                StopCommand = new RelayCommand(_ => Stop());
                ToggleMonitoringCommand = new RelayCommand(_ => ToggleMonitoring());
                ExportCsvCommand = new RelayCommand(_ => ExportCsv());
                ExportTopHostsCsvCommand = new RelayCommand(_ => ExportTopHostsCsv());
                ShowMonitoringTabCommand = new RelayCommand(_ => SelectedMainTabIndex = 0);
                ShowAnalyticsTabCommand = new RelayCommand(_ => SelectedMainTabIndex = 1);
                RunNowCommand = new RelayCommand(_ => RunNow(), _ => _isMonitoring);
                Logger.Write("ViewModel: Команды ОК");

                _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _progressTimer.Tick += (_, _) => UpdateProgressTick();

                Logger.Write("Основная Виев-Модель инициализирована");
            }
            catch (Exception ex)
            {
                Logger.Write("ОШИБКА ViewModel: " + ex.GetType().Name + " | " + ex.Message + " | Stack: " + ex.StackTrace);
                throw;
            }
        }

        private void Monitor_NewResult(object? sender, SpeedtestResult r)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    CurrentIp = r.Ip;
                    CurrentCountry = r.Country;
                    CurrentAsn = string.IsNullOrWhiteSpace(r.Asn) ? "N/A" : r.Asn;

                    var entry = new ResultEntry
                    {
                        Ip = r.Ip,
                        Country = r.Country,
                        Timestamp = r.Timestamp.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"),
                        Ping = Math.Round(r.Ping, 2),
                        Jitter = Math.Round(r.Jitter, 2),
                        Loss = Math.Round(r.Loss, 2),
                        Download = Math.Round(r.Download, 2),
                        Upload = Math.Round(r.Upload, 2)
                    };
                    entry.Score = _scoring.Calculate(entry);
                    entry.ScoreDetails = _scoring.BuildDetails(entry);

                    _resultsManager.AddResult(entry);

                    UpdateRecommendation();
                    UpdateTopHosts();
                    UpdateHostAnalytics();
                    _hadSuccessfulMeasurement = true;
                    _lastSuccessUtc = DateTime.UtcNow;
                    SetStatus(StatusKind.Ok, $"Последний замер: {DateTime.Now:HH:mm:ss}");

                    NewResultArrived?.Invoke(this, r);
                }
                catch (Exception ex)
                {
                    Logger.Write($"Monitor_NewResult error: {ex.Message}");
                    SetStatus(StatusKind.Error, "Ошибка обновления результата");
                }
            });
        }

        private void Monitor_IpInfoUpdated(object? sender, IpInfo info)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentIp = info.Ip;
                CurrentCountry = string.IsNullOrWhiteSpace(info.CountryName) ? CurrentCountry : info.CountryName;
                CurrentAsn = string.IsNullOrWhiteSpace(info.Asn) ? "N/A" : info.Asn;
            });
        }

        private void Monitor_VpnProcessInfoUpdated(object? sender, string text)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                VpnProcessInfoText = string.IsNullOrWhiteSpace(text) ? "процесс: —" : text;
            });
        }

        private void Monitor_StatusMessage(object? sender, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (TryParseStatus(message, out var kind, out var text))
                {
                    SetStatus(kind, text);
                    return;
                }

                // Защитный путь на случай неизвестного формата.
                SetStatus(StatusKind.Wait, message);
            });
        }

        /// <summary>
        /// Разбирает сообщение вида "KIND:текст" в типизированный статус.
        /// Поддерживаемые префиксы: OK, CHECK, WAIT, ERROR.
        /// </summary>
        private static bool TryParseStatus(string message, out StatusKind kind, out string text)
        {
            kind = StatusKind.Wait;
            text = string.Empty;

            if (string.IsNullOrEmpty(message))
                return false;

            var separatorIndex = message.IndexOf(':');
            if (separatorIndex <= 0)
                return false;

            var prefix = message.Substring(0, separatorIndex).Trim().ToUpperInvariant();
            var body = message.Substring(separatorIndex + 1).Trim();

            switch (prefix)
            {
                case "OK":
                    kind = StatusKind.Ok;
                    text = body;
                    return true;
                case "CHECK":
                case "CHECKING":
                    kind = StatusKind.Check;
                    text = body;
                    return true;
                case "WAIT":
                case "INFO":
                    kind = StatusKind.Wait;
                    text = body;
                    return true;
                case "ERROR":
                    kind = StatusKind.Error;
                    text = body;
                    return true;
                default:
                    return false;
            }
        }

        private void SetStatus(StatusKind kind, string text)
        {
            StatusText = text;
            StatusColor = kind switch
            {
                StatusKind.Ok => StatusColorOk,
                StatusKind.Check => StatusColorCheck,
                StatusKind.Error => StatusColorError,
                _ => StatusColorWait
            };

            UpdateProgressForStatus(kind);
        }

        private void UpdateProgressForStatus(StatusKind kind)
        {
            ProgressColor = kind switch
            {
                StatusKind.Ok => StatusColorOk,
                StatusKind.Check => StatusColorCheck,
                StatusKind.Error => StatusColorError,
                _ => StatusColorWait
            };

            switch (kind)
            {
                case StatusKind.Wait:
                    ProgressIsIndeterminate = false;
                    ApplyProgressValue(0);
                    ProgressVisible = false;
                    StopProgressTimer();
                    break;

                case StatusKind.Check:
                    // Реальный ход замера показываем по stderr (+ heartbeat); «бесконечная» полоса убрана сознательно.
                    ProgressIsIndeterminate = false;
                    ApplyProgressValue(3);
                    ProgressVisible = true;
                    StopProgressTimer();
                    break;

                case StatusKind.Ok:
                    ProgressIsIndeterminate = false;
                    ProgressVisible = true;
                    if (_hadSuccessfulMeasurement && _lastSuccessUtc != default)
                    {
                        UpdateProgressTick();
                        StartProgressTimer();
                    }
                    else
                    {
                        ApplyProgressValue(0);
                        StopProgressTimer();
                    }

                    break;

                case StatusKind.Error:
                    ProgressIsIndeterminate = false;
                    ApplyProgressValue(100);
                    ProgressVisible = true;
                    StopProgressTimer();
                    break;
            }
        }

        private void StartProgressTimer()
        {
            if (!_progressTimer.IsEnabled)
                _progressTimer.Start();
        }

        private void StopProgressTimer()
        {
            if (_progressTimer.IsEnabled)
                _progressTimer.Stop();
        }

        /// <summary>
        /// Считает процент пройденного времени между замерами и обновляет индикатор.
        /// </summary>
        private void UpdateProgressTick()
        {
            if (!ProgressVisible || ProgressIsIndeterminate)
                return;

            if (!_hadSuccessfulMeasurement || _lastSuccessUtc == default)
            {
                ApplyProgressValue(0);
                return;
            }

            var elapsed = DateTime.UtcNow - _lastSuccessUtc;
            var ratio = elapsed.TotalSeconds / ProgressFullPeriod.TotalSeconds;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;
            ApplyProgressValue(ratio * 100.0);
        }

        /// <summary>
        /// Выставляет Value прогрессбара без порога отсечения (иначе шкала между замерами может не двигаться).
        /// </summary>
        private void ApplyProgressValue(double value)
        {
            value = Math.Clamp(value, 0, 100);
            if (Math.Abs(_progressValue - value) < 0.02)
                return;

            _progressValue = value;
            NotifyPropertyChanged(nameof(ProgressValue));
        }

        /// <summary>
        /// Процент хода speedtest с фонового потока.
        /// </summary>
        private void Monitor_SpeedtestProgress(object? sender, double percent)
        {
            _ = Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ProgressVisible = true;
                ProgressIsIndeterminate = false;
                ProgressColor = StatusColorCheck;
                ApplyProgressValue(percent);
            }));
        }

        private enum StatusKind
        {
            Wait,
            Check,
            Ok,
            Error
        }

        public void Start()
        {
            try
            {
                if (_isMonitoring)
                {
                    Logger.Write("Monitor already running");
                    return;
                }

                _isMonitoring = true;
                _hadSuccessfulMeasurement = false;
                _lastSuccessUtc = default;
                SetStatus(StatusKind.Ok, "Мониторинг запущен");
                _monitor.Start();
                NotifyPropertyChanged(nameof(ToggleMonitoringButtonText));
                RunNowCommand.RaiseCanExecuteChanged();
                Logger.Write("Monitoring started");
            }
            catch (Exception ex)
            {
                Logger.Write($"Start error: {ex.Message}");
                SetStatus(StatusKind.Error, "Ошибка запуска");
                MessageBox.Show($"Не удалось запустить мониторинг: {ex.Message}", "VPN Speed Analyzer");
            }
        }

        public void Stop()
        {
            try
            {
                if (!_isMonitoring)
                {
                    Logger.Write("Monitor not running");
                    return;
                }

                _isMonitoring = false;
                _hadSuccessfulMeasurement = false;
                _lastSuccessUtc = default;
                SetStatus(StatusKind.Wait, "Остановлено");
                _monitor.Stop();
                NotifyPropertyChanged(nameof(ToggleMonitoringButtonText));
                RunNowCommand.RaiseCanExecuteChanged();
                Logger.Write("Monitoring stopped");
            }
            catch (Exception ex)
            {
                Logger.Write($"Stop error: {ex.Message}");
                SetStatus(StatusKind.Error, "Ошибка остановки");
            }
        }

        public void ToggleMonitoring()
        {
            if (_isMonitoring)
                Stop();
            else
                Start();
        }

        /// <summary>
        /// Просит контроллер сделать внеплановый замер прямо сейчас.
        /// </summary>
        public void RunNow()
        {
            if (!_isMonitoring)
                return;

            _monitor.RequestImmediateRun();
            SetStatus(StatusKind.Check, "Замер скорости (по запросу)");
        }

        public void ExportCsv()
        {
            if (Results.Count == 0)
            {
                MessageBox.Show("Пока нет данных для экспорта.", "Экспорт");
                return;
            }

            var path = AskCsvPath($"results_{DateTime.Now:yyyyMMdd_HHmmss}.csv", "Сохранить общий CSV");
            if (path == null)
                return;

            try
            {
                using (var sw = new StreamWriter(path))
                {
                    sw.WriteLine("Timestamp,IP,Country,Ping,Jitter,Loss,Download,Upload,DQS");

                    foreach (var r in Results)
                    {
                        var timestamp = EscapeCsvField(r.Timestamp);
                        var ip = EscapeCsvField(r.Ip);
                        var country = EscapeCsvField(r.Country);

                        sw.WriteLine($"{timestamp},{ip},{country},{r.Ping},{r.Jitter},{r.Loss},{r.Download},{r.Upload},{r.Score}");
                    }
                }

                Logger.Write($"CSV экспортирован: {path}");
                MessageBox.Show($"CSV сохранён:\n{path}", "Экспорт");
            }
            catch (Exception ex)
            {
                Logger.Write($"ExportCsv error: {ex.Message}");
                MessageBox.Show($"Не удалось сохранить CSV: {ex.Message}", "Ошибка экспорта");
            }
        }

        public void ExportTopHostsCsv()
        {
            if (TopHosts.Count == 0)
            {
                MessageBox.Show("Пока нет данных рейтинга для экспорта.", "Экспорт");
                return;
            }

            var path = AskCsvPath($"top_hosts_{DateTime.Now:yyyyMMdd_HHmmss}.csv", "Сохранить рейтинг хостов");
            if (path == null)
                return;

            try
            {
                using (var sw = new StreamWriter(path))
                {
                    sw.WriteLine("Rank,IP,Country,Ping,Jitter,Loss,Download,Upload,DQS");

                    foreach (var r in TopHosts)
                    {
                        var ip = EscapeCsvField(r.Ip);
                        var country = EscapeCsvField(r.Country);
                        sw.WriteLine($"{r.Rank},{ip},{country},{r.Ping},{r.Jitter},{r.Loss},{r.Download},{r.Upload},{r.Score}");
                    }
                }

                Logger.Write($"Top hosts CSV экспортирован: {path}");
                MessageBox.Show($"CSV сохранён:\n{path}", "Экспорт");
            }
            catch (Exception ex)
            {
                Logger.Write($"ExportTopHostsCsv error: {ex.Message}");
                MessageBox.Show($"Не удалось сохранить CSV: {ex.Message}", "Ошибка экспорта");
            }
        }

        /// <summary>
        /// Открывает SaveFileDialog для выбора места сохранения CSV-файла.
        /// </summary>
        private static string? AskCsvPath(string defaultFileName, string title)
        {
            var dlg = new SaveFileDialog
            {
                Title = title,
                FileName = defaultFileName,
                DefaultExt = ".csv",
                Filter = "CSV файлы (*.csv)|*.csv|Все файлы (*.*)|*.*",
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        /// <summary>
        /// Экрранирует часть CSV для защиты от атак injection
        /// </summary>
        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "\"\"";

            // Проверяем потенциально опасные префиксы формул.
            if (field.Length > 0 && (
                field[0] == '=' ||
                field[0] == '+' ||
                field[0] == '-' ||
                field[0] == '@' ||
                field[0] == '\t' ||
                field[0] == '\r'))
            {
                field = "'" + field;
            }

            // Экранируем кавычки и оборачиваем поле при необходимости.
            if (field.Contains("\"") || field.Contains(",") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }

        private void RecalculateScores()
        {
            _resultsManager.RecalculateScores(_scoring.Calculate, _scoring.BuildDetails);
            UpdateRecommendation();
            UpdateTopHosts();
            UpdateHostAnalytics();
            NotifyPropertyChanged(nameof(SelectedResult));
        }

        private void UpdateRecommendation()
        {
            var best = _resultsManager.GetRecommendedResult();
            if (best == null)
            {
                RecommendationText = "Рекомендация появится после первого успешного замера";
                return;
            }

            RecommendationText = $"Лучший хост сейчас: {best.Country} ({best.Ip}), D.Q.S {best.Score}";
        }

        private void UpdateTopHosts()
        {
            foreach (var entry in Results)
            {
                entry.Rank = 0;
                entry.RankMarker = "";
                entry.RankMarkerColor = "#A8B0D9";
            }

            var rankedResults = _resultsManager.GetTopResults(Results.Count);
            var rank = 1;
            foreach (var item in rankedResults)
            {
                item.Rank = rank++;
                item.RankMarker = "●";
                item.RankMarkerColor = item.Rank switch
                {
                    1 => "#F6C453",
                    2 => "#B8C0D8",
                    3 => "#D08A5C",
                    _ => "#A8B0D9"
                };

                if (item.Rank > TopHostsCount)
                {
                    item.RankMarker = "";
                }
            }

            TopHosts.Clear();
            foreach (var topItem in rankedResults.Take(TopHostsCount))
                TopHosts.Add(topItem);
        }

        private void UpdateHostAnalytics()
        {
            if (Results.Count == 0)
            {
                _selectedHostRatingRow = null;
                NotifyPropertyChanged(nameof(SelectedHostRatingRow));
                HostRatings.Clear();
                NotifyPropertyChanged(nameof(TotalMeasurements));
                NotifyPropertyChanged(nameof(UniqueHostsCount));
                RefreshLeaderComparison();
                RatingSummaryText = "Недостаточно данных для аналитики";
                StabilitySummaryText = "Стабильность будет рассчитана после нескольких замеров";
                BestHostSummaryText = "Лучший хост появится после первого успешного замера";
                return;
            }

            var grouped = Results
                .GroupBy(r => $"{r.Ip}|{r.Country}")
                .Select(g =>
                {
                    var list = g.ToList();
                    var sampleCount = list.Count;
                    var best = list.Max(x => x.Score);
                    var avgScore = Math.Round(list.Average(x => x.Score), 2);
                    var avgPing = Math.Round(list.Average(x => x.Ping), 2);
                    var avgJitter = Math.Round(list.Average(x => x.Jitter), 2);
                    var avgLoss = Math.Round(list.Average(x => x.Loss), 2);
                    var avgDl = Math.Round(list.Average(x => x.Download), 2);
                    var avgUl = Math.Round(list.Average(x => x.Upload), 2);
                    var latest = list[0];

                    return new HostRatingRow
                    {
                        Ip = latest.Ip,
                        Country = latest.Country,
                        Samples = sampleCount,
                        AverageScore = avgScore,
                        BestScore = Math.Round(best, 2),
                        AveragePing = avgPing,
                        AverageJitter = avgJitter,
                        AverageLoss = avgLoss,
                        AverageDownloadMbps = avgDl,
                        AverageUploadMbps = avgUl
                    };
                })
                .OrderByDescending(x => x.AverageScore)
                .ThenBy(x => x.AveragePing)
                .ToList();

            _selectedHostRatingRow = null;
            NotifyPropertyChanged(nameof(SelectedHostRatingRow));
            HostRatings.Clear();

            var rank = 1;
            foreach (var host in grouped)
            {
                host.Rank = rank++;
                HostRatings.Add(host);
            }

            NotifyPropertyChanged(nameof(TotalMeasurements));
            NotifyPropertyChanged(nameof(UniqueHostsCount));

            var bestHost = HostRatings.FirstOrDefault();
            var worstHost = HostRatings.LastOrDefault();
            if (bestHost == null || worstHost == null)
            {
                RatingSummaryText = "Недостаточно данных для аналитики";
                StabilitySummaryText = "Стабильность будет рассчитана после нескольких замеров";
                BestHostSummaryText = "Лучший хост появится после первого успешного замера";
                RefreshLeaderComparison();
                return;
            }

            var scoreDelta = Math.Round(bestHost.AverageScore - worstHost.AverageScore, 2);
            RatingSummaryText = $"Разброс среднего D.Q.S между лучшим и худшим хостом: {scoreDelta:F2}";

            var avgJitterByHostMedian = Math.Round(Median(HostRatings.Select(h => h.AverageJitter).ToList()), 2);
            var avgLossByHostMedian = Math.Round(Median(HostRatings.Select(h => h.AverageLoss).ToList()), 2);
            StabilitySummaryText =
                $"Медиана по хостам (ср. джиттер / ср. потери): {avgJitterByHostMedian:F2} мс, {avgLossByHostMedian:F2}%";

            BestHostSummaryText =
                $"Лидер: {bestHost.Country} ({bestHost.Ip}) — ср. D.Q.S {bestHost.AverageScore:F2} по {bestHost.Samples} замерам, " +
                $"↓ {bestHost.AverageDownloadMbps:F1} / ↑ {bestHost.AverageUploadMbps:F1} Мбит/с";

            RefreshLeaderComparison();
        }

        /// <summary>
        /// KPI аналитики: значение лидера таблицы и при выборе строки — Δ к лидеру (цвет по направлению «лучше/хуже»).
        /// </summary>
        private void RefreshLeaderComparison()
        {
            var leader = HostRatings.FirstOrDefault();
            var sel = _selectedHostRatingRow;

            if (leader == null)
            {
                _cmpDqsLeaderText = "";
                _cmpPingLeaderText = "";
                _cmpJitterLeaderText = "";
                _cmpLossLeaderText = "";
                _cmpDownLeaderText = "";
                _cmpUpLeaderText = "";
                BlankComparisonDeltas();
                NotifyLeaderComparisonProps();
                return;
            }

            var inv = CultureInfo.InvariantCulture;
            _cmpDqsLeaderText = leader.AverageScore.ToString("F2", inv);
            _cmpPingLeaderText = leader.AveragePing.ToString("F2", inv);
            _cmpJitterLeaderText = leader.AverageJitter.ToString("F2", inv);
            _cmpLossLeaderText = leader.AverageLoss.ToString("F2", inv);
            _cmpDownLeaderText = leader.AverageDownloadMbps.ToString("F2", inv);
            _cmpUpLeaderText = leader.AverageUploadMbps.ToString("F2", inv);

            if (sel == null)
            {
                BlankComparisonDeltas();
                NotifyLeaderComparisonProps();
                return;
            }

            ComputeDirectedDelta(sel.AverageScore, leader.AverageScore, higherIsBetter: true, "F2", 0.01, out _cmpDqsDeltaText, out _cmpDqsDeltaBrush);
            ComputeDirectedDelta(sel.AveragePing, leader.AveragePing, higherIsBetter: false, "F2", 0.01, out _cmpPingDeltaText, out _cmpPingDeltaBrush);
            ComputeDirectedDelta(sel.AverageJitter, leader.AverageJitter, higherIsBetter: false, "F2", 0.01, out _cmpJitterDeltaText, out _cmpJitterDeltaBrush);
            ComputeDirectedDelta(sel.AverageLoss, leader.AverageLoss, higherIsBetter: false, "F2", 0.01, out _cmpLossDeltaText, out _cmpLossDeltaBrush);
            ComputeDirectedDelta(sel.AverageDownloadMbps, leader.AverageDownloadMbps, higherIsBetter: true, "F2", 0.05, out _cmpDownDeltaText, out _cmpDownDeltaBrush);
            ComputeDirectedDelta(sel.AverageUploadMbps, leader.AverageUploadMbps, higherIsBetter: true, "F2", 0.05, out _cmpUpDeltaText, out _cmpUpDeltaBrush);

            NotifyLeaderComparisonProps();
        }

        private void BlankComparisonDeltas()
        {
            _cmpDqsDeltaText = "";
            _cmpDqsDeltaBrush = CmpBrushNeutral;
            _cmpPingDeltaText = "";
            _cmpPingDeltaBrush = CmpBrushNeutral;
            _cmpJitterDeltaText = "";
            _cmpJitterDeltaBrush = CmpBrushNeutral;
            _cmpLossDeltaText = "";
            _cmpLossDeltaBrush = CmpBrushNeutral;
            _cmpDownDeltaText = "";
            _cmpDownDeltaBrush = CmpBrushNeutral;
            _cmpUpDeltaText = "";
            _cmpUpDeltaBrush = CmpBrushNeutral;
        }

        private static string BuildComparisonSeparator(string left, string right)
        {
            return string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right) ? "" : "|";
        }

        private void NotifyLeaderComparisonProps()
        {
            NotifyPropertyChanged(nameof(CmpDqsLeaderText));
            NotifyPropertyChanged(nameof(CmpDqsDeltaText));
            NotifyPropertyChanged(nameof(CmpDqsSeparatorText));
            NotifyPropertyChanged(nameof(CmpDqsDeltaBrush));
            NotifyPropertyChanged(nameof(CmpPingLeaderText));
            NotifyPropertyChanged(nameof(CmpPingDeltaText));
            NotifyPropertyChanged(nameof(CmpPingSeparatorText));
            NotifyPropertyChanged(nameof(CmpPingDeltaBrush));
            NotifyPropertyChanged(nameof(CmpJitterLeaderText));
            NotifyPropertyChanged(nameof(CmpJitterDeltaText));
            NotifyPropertyChanged(nameof(CmpJitterSeparatorText));
            NotifyPropertyChanged(nameof(CmpJitterDeltaBrush));
            NotifyPropertyChanged(nameof(CmpLossLeaderText));
            NotifyPropertyChanged(nameof(CmpLossDeltaText));
            NotifyPropertyChanged(nameof(CmpLossSeparatorText));
            NotifyPropertyChanged(nameof(CmpLossDeltaBrush));
            NotifyPropertyChanged(nameof(CmpDownLeaderText));
            NotifyPropertyChanged(nameof(CmpDownDeltaText));
            NotifyPropertyChanged(nameof(CmpDownSeparatorText));
            NotifyPropertyChanged(nameof(CmpDownDeltaBrush));
            NotifyPropertyChanged(nameof(CmpUpLeaderText));
            NotifyPropertyChanged(nameof(CmpUpDeltaText));
            NotifyPropertyChanged(nameof(CmpUpSeparatorText));
            NotifyPropertyChanged(nameof(CmpUpDeltaBrush));
        }

        /// <summary>
        /// Δ = выбранный − лидер; плюс красим зелёным, минус — красным.
        /// </summary>
        private static void ComputeDirectedDelta(
            double selectedValue,
            double leaderValue,
            bool higherIsBetter,
            string format,
            double epsilon,
            out string deltaText,
            out Brush deltaBrush)
        {
            var d = selectedValue - leaderValue;
            if (Math.Abs(d) < epsilon)
            {
                deltaText = format == "F2" ? "+0.00" : "+0";
                deltaBrush = CmpBrushNeutral;
                return;
            }

            var inv = CultureInfo.InvariantCulture;
            var body = d > 0
                ? "+" + d.ToString(format, inv)
                : "\u2212" + (-d).ToString(format, inv);

            deltaText = body;
            var isBetter = higherIsBetter ? d > 0 : d < 0;
            deltaBrush = isBetter ? CmpBrushGood : CmpBrushBad;
        }

        private static SolidColorBrush CreateCmpBrushStatic(string hex)
        {
            var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            b.Freeze();
            return b;
        }

        /// <summary>
        /// Медиана по уже извлечённым значениям (устойчива к выбросам).
        /// </summary>
        private static double Median(IReadOnlyList<double> values)
        {
            if (values.Count == 0)
                return 0;

            var sorted = values.OrderBy(x => x).ToList();
            int mid = sorted.Count / 2;
            if ((sorted.Count & 1) == 1)
                return sorted[mid];

            return (sorted[mid - 1] + sorted[mid]) / 2.0;
        }

        private void NotifyPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
