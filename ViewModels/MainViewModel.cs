using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using VpnSpeedAnalyzer.Logic;
using VpnSpeedAnalyzer.Models;
using VpnSpeedAnalyzer.Services;

namespace VpnSpeedAnalyzer
{
    /// <summary>
    /// Основная Виев-Модель приложения
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private const string ProfileUniversal = "Универсальный";
        private const string ProfileGaming = "Игры";
        private const string ProfileStreaming = "Стрим";
        private const int TopHostsCount = 5;

        private readonly MonitorController _monitor;
        private readonly ResultsManager _resultsManager;

        private string _currentIp = "";
        private string _currentCountry = "";
        private string _currentAsn = "";
        private string _statusText = "Остановлено";
        private string _statusColor = "#A8B0D9";
        private string _selectedScoringProfile = ProfileUniversal;
        private string _recommendationText = "Рекомендация появится после первого успешного замера";
        private ResultEntry? _selectedResult;
        private bool _isMonitoring;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<SpeedtestResult>? NewResultArrived;

        public ObservableCollection<ResultEntry> Results { get; } = new();
        public ObservableCollection<ResultEntry> TopHosts { get; } = new();
        public ObservableCollection<string> ScoringProfiles { get; } = new()
        {
            ProfileUniversal,
            ProfileGaming,
            ProfileStreaming
        };

        public double ScoringProfileComboWidth
        {
            get
            {
                var longestProfileLength = 0;
                foreach (var profile in ScoringProfiles)
                {
                    if (!string.IsNullOrEmpty(profile) && profile.Length > longestProfileLength)
                        longestProfileLength = profile.Length;
                }

                // Примерная ширина текста + внутренние отступы и зона стрелки.
                var calculatedWidth = longestProfileLength * 9 + 56;
                return Math.Max(120, calculatedWidth);
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
                    RecalculateScores();
                    NotifyPropertyChanged(nameof(SelectedScoringProfile));
                    NotifyPropertyChanged(nameof(SelectedScoringProfileDescription));
                }
            }
        }

        /// <summary>
        /// Описание активного профиля D.Q.S для подсказок в UI.
        /// </summary>
        public string SelectedScoringProfileDescription => GetProfileDescription(_selectedScoringProfile);

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
        public ICommand ToggleBestOnlyCommand { get; }

        public string ToggleMonitoringButtonText => _isMonitoring ? "Стоп" : "Старт";
        public string ToggleBestOnlyButtonText => _resultsManager.IsBestOnlyMode ? "Все результаты" : "Только лучшие";

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
                _monitor.StatusMessage += Monitor_StatusMessage;
                Logger.Write("ViewModel: Monitor.StatusMessage подписана");

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
                ToggleBestOnlyCommand = new RelayCommand(_ => ToggleBestOnly());
                Logger.Write("ViewModel: Команды ОК");

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
                    entry.Score = CalculateQualityScore(entry);
                    entry.ScoreDetails = BuildScoreDetails(entry);

                    _resultsManager.AddResult(entry);

                    UpdateRecommendation();
                    UpdateTopHosts();
                    SelectedResult ??= _resultsManager.GetRecommendedResult();

                    StatusText = $"✓ Последняя проверка: {DateTime.Now:HH:mm:ss}";
                    StatusColor = "#59D9B7";

                    NewResultArrived?.Invoke(this, r);
                }
                catch (Exception ex)
                {
                    Logger.Write($"Monitor_NewResult error: {ex.Message}");
                    StatusText = "Ошибка обновления результата";
                    StatusColor = "#FF7AA2";
                }
            });
        }

        private void Monitor_StatusMessage(object? sender, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                const string errorPrefix = "ERROR:";
                const string checkingPrefix = "CHECKING:";
                const string infoPrefix = "INFO:";

                if (message.StartsWith(errorPrefix, StringComparison.Ordinal))
                {
                    StatusText = message.Substring(errorPrefix.Length).Trim();
                    StatusColor = "#FF7AA2";
                    return;
                }

                if (message.StartsWith(checkingPrefix, StringComparison.Ordinal))
                {
                    StatusText = message.Substring(checkingPrefix.Length).Trim();
                    StatusColor = "#F6C453";
                    return;
                }

                if (message.StartsWith(infoPrefix, StringComparison.Ordinal))
                {
                    StatusText = message.Substring(infoPrefix.Length).Trim();
                    StatusColor = "#A8B0D9";
                    return;
                }

                StatusText = message;
                StatusColor = "#A8B0D9";
            });
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
                StatusText = "Мониторинг запущен";
                StatusColor = "#59D9B7";
                _monitor.Start();
                NotifyPropertyChanged(nameof(ToggleMonitoringButtonText));
                Logger.Write("Monitoring started");
            }
            catch (Exception ex)
            {
                Logger.Write($"Start error: {ex.Message}");
                StatusText = "Ошибка запуска";
                StatusColor = "#FF7AA2";
                MessageBox.Show($"Error starting monitor: {ex.Message}");
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
                StatusText = "Остановлено";
                StatusColor = "#A8B0D9";
                _monitor.Stop();
                NotifyPropertyChanged(nameof(ToggleMonitoringButtonText));
                Logger.Write("Monitoring stopped");
            }
            catch (Exception ex)
            {
                Logger.Write($"Stop error: {ex.Message}");
                StatusText = "Ошибка остановки";
                StatusColor = "#FF7AA2";
            }
        }

        public void ToggleBestOnly()
        {
            try
            {
                _resultsManager.ToggleBestOnly();
                NotifyPropertyChanged(nameof(ToggleBestOnlyButtonText));
                UpdateRecommendation();
                UpdateTopHosts();
                Logger.Write("Best results filter applied");
            }
            catch (Exception ex)
            {
                Logger.Write($"ToggleBestOnly error: {ex.Message}");
                MessageBox.Show($"Error applying filter: {ex.Message}");
            }
        }

        public void ToggleMonitoring()
        {
            if (_isMonitoring)
                Stop();
            else
                Start();
        }

        public void ExportCsv()
        {
            try
            {
                if (Results.Count == 0)
                {
                    MessageBox.Show("No results to export", "Info");
                    return;
                }

                string path = $"results_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

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

                Logger.Write($"CSV exported to {path}");
                MessageBox.Show($"CSV saved: {path}", "Success");
            }
            catch (Exception ex)
            {
                Logger.Write($"ExportCsv error: {ex.Message}");
                MessageBox.Show($"Error exporting CSV: {ex.Message}", "Error");
            }
        }

        public void ExportTopHostsCsv()
        {
            try
            {
                if (TopHosts.Count == 0)
                {
                    MessageBox.Show("Нет данных рейтинга для экспорта", "Info");
                    return;
                }

                string path = $"top_hosts_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

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

                Logger.Write($"Top hosts CSV exported to {path}");
                MessageBox.Show($"CSV saved: {path}", "Success");
            }
            catch (Exception ex)
            {
                Logger.Write($"ExportTopHostsCsv error: {ex.Message}");
                MessageBox.Show($"Error exporting top hosts CSV: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// Экрранирует часть CSV для защиты от атак injection
        /// </summary>
        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "\"\"";

            // Check for formula injection patterns
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

            // Escape quotes and wrap if needed
            if (field.Contains("\"") || field.Contains(",") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }

        private void RecalculateScores()
        {
            _resultsManager.RecalculateScores(CalculateQualityScore, BuildScoreDetails);
            UpdateRecommendation();
            UpdateTopHosts();
            NotifyPropertyChanged(nameof(SelectedResult));
        }

        /// <summary>
        /// Рассчитывает качество хоста по шкале 0..100.
        /// Чем ниже ping/jitter/loss и выше скорости, тем выше итоговый балл.
        /// </summary>
        private double CalculateQualityScore(ResultEntry result)
        {
            var pingScore = ScoreLowerIsBetter(result.Ping, ideal: 20, worst: 150);
            var jitterScore = ScoreLowerIsBetter(result.Jitter, ideal: 2, worst: 40);
            var lossScore = ScoreLowerIsBetter(result.Loss, ideal: 0, worst: 5);
            var downloadScore = ScoreHigherIsBetter(result.Download, ideal: 400, worst: 20);
            var uploadScore = ScoreHigherIsBetter(result.Upload, ideal: 150, worst: 10);

            var (pingWeight, jitterWeight, lossWeight, downloadWeight, uploadWeight) = GetProfileWeights();

            var score =
                pingScore * pingWeight +
                jitterScore * jitterWeight +
                lossScore * lossWeight +
                downloadScore * downloadWeight +
                uploadScore * uploadWeight;

            return Math.Round(score, 2);
        }

        private string BuildScoreDetails(ResultEntry result)
        {
            return $"{SelectedScoringProfile}: ping {Math.Round(result.Ping, 2)} мс, дрожание {Math.Round(result.Jitter, 2)} мс, потери {Math.Round(result.Loss, 2)}%, загрузка {Math.Round(result.Download, 2)} Мбит/с, отдача {Math.Round(result.Upload, 2)} Мбит/с";
        }

        private (double ping, double jitter, double loss, double download, double upload) GetProfileWeights()
        {
            return SelectedScoringProfile switch
            {
                ProfileGaming => (0.42, 0.28, 0.20, 0.06, 0.04),
                ProfileStreaming => (0.20, 0.10, 0.15, 0.35, 0.20),
                _ => (0.30, 0.20, 0.25, 0.15, 0.10)
            };
        }

        private static string GetProfileDescription(string profileName)
        {
            return profileName switch
            {
                ProfileGaming => "Игры: максимальный приоритет низкому пингу, дрожанию и потерям. Скорости учитываются меньше.",
                ProfileStreaming => "Стрим: повышенный вес загрузки и отдачи при сохранении умеренных требований к задержке.",
                _ => "Универсальный: сбалансированный профиль для общего использования интернета и VPN."
            };
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
            TopHosts.Clear();
            var rank = 1;
            foreach (var item in _resultsManager.GetTopResults(TopHostsCount))
            {
                item.Rank = rank++;
                item.RankBadge = item.Rank switch
                {
                    1 => "🥇",
                    2 => "🥈",
                    3 => "🥉",
                    _ => ""
                };
                item.RankMarker = "●";
                item.RankMarkerColor = item.Rank switch
                {
                    1 => "#F6C453",
                    2 => "#B8C0D8",
                    3 => "#D08A5C",
                    _ => "#5E668F"
                };
                TopHosts.Add(item);
            }
        }

        private static double ScoreLowerIsBetter(double value, double ideal, double worst)
        {
            if (value <= ideal) return 100;
            if (value >= worst) return 0;

            var ratio = (value - ideal) / (worst - ideal);
            return Math.Round((1 - ratio) * 100, 2);
        }

        private static double ScoreHigherIsBetter(double value, double ideal, double worst)
        {
            if (value >= ideal) return 100;
            if (value <= worst) return 0;

            var ratio = (value - worst) / (ideal - worst);
            return Math.Round(ratio * 100, 2);
        }

        private void NotifyPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
