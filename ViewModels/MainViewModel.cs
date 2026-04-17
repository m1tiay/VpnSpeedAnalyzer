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
        private readonly MonitorController _monitor;
        private readonly ResultsManager _resultsManager;

        private string _currentIp = "";
        private string _currentCountry = "";
        private string _currentAsn = "";
        private string _statusText = "Остановлено";
        private bool _isMonitoring;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<SpeedtestResult>? NewResultArrived;

        public ObservableCollection<ResultEntry> Results { get; } = new();

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

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ToggleBestOnlyCommand { get; }

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

                Logger.Write("ViewModel: ResultsManager создание");
                // Создаём менеджер результатов
                _resultsManager = new ResultsManager(Results);
                Logger.Write("ViewModel: ResultsManager ОК");

                Logger.Write("ViewModel: Команды создание");
                // Создаём команды
                StartCommand = new RelayCommand(_ => Start());
                StopCommand = new RelayCommand(_ => Stop());
                ExportCsvCommand = new RelayCommand(_ => ExportCsv());
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
                    CurrentAsn = r.Ip; // TODO: Extract ASN from IP if available

                    Results.Add(new ResultEntry
                    {
                        Ip = r.Ip,
                        Country = r.Country,
                        Timestamp = r.Timestamp.ToString("O"),
                        Ping = r.Ping,
                        Jitter = r.Jitter,
                        Loss = r.Loss,
                        Download = r.Download,
                        Upload = r.Upload
                    });

                    StatusText = $"✓ Last check: {DateTime.Now:HH:mm:ss}";

                    NewResultArrived?.Invoke(this, r);
                }
                catch (Exception ex)
                {
                    Logger.Write($"Monitor_NewResult error: {ex.Message}");
                }
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
                StatusText = "Running...";
                _monitor.Start();
                Logger.Write("Monitoring started");
            }
            catch (Exception ex)
            {
                Logger.Write($"Start error: {ex.Message}");
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
                StatusText = "Stopped";
                _monitor.Stop();
                Logger.Write("Monitoring stopped");
            }
            catch (Exception ex)
            {
                Logger.Write($"Stop error: {ex.Message}");
            }
        }

        public void ToggleBestOnly()
        {
            try
            {
                _resultsManager.ApplyBestOnly();
                Logger.Write("Best results filter applied");
            }
            catch (Exception ex)
            {
                Logger.Write($"ToggleBestOnly error: {ex.Message}");
                MessageBox.Show($"Error applying filter: {ex.Message}");
            }
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
                    sw.WriteLine("Timestamp,IP,Country,Ping,Jitter,Loss,Download,Upload");

                    foreach (var r in Results)
                    {
                        var timestamp = EscapeCsvField(r.Timestamp);
                        var ip = EscapeCsvField(r.Ip);
                        var country = EscapeCsvField(r.Country);

                        sw.WriteLine($"{timestamp},{ip},{country},{r.Ping},{r.Jitter},{r.Loss},{r.Download},{r.Upload}");
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

        private void NotifyPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
