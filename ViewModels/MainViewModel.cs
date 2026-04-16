using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace VpnSpeedAnalyzer
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<SpeedtestResult>? NewResultArrived;

        private readonly MonitorController _monitor;

        public ObservableCollection<SpeedtestResult> Results { get; } = new();

        private bool _showBestOnly;
        public bool ShowBestOnly
        {
            get => _showBestOnly;
            set
            {
                if (_showBestOnly != value)
                {
                    _showBestOnly = value;
                    OnPropertyChanged(nameof(ShowBestOnly));
                    RefreshResults();
                }
            }
        }

        public RelayCommand StartCommand { get; }
        public RelayCommand StopCommand { get; }
        public RelayCommand ExportCsvCommand { get; }
        public RelayCommand ToggleBestOnlyCommand { get; }

        public MainViewModel()
        {
            _monitor = new MonitorController();
            _monitor.NewResult += Monitor_NewResult;

            StartCommand = new RelayCommand(_ => Start());
            StopCommand = new RelayCommand(_ => Stop());
            ExportCsvCommand = new RelayCommand(_ => ExportCsv());
            ToggleBestOnlyCommand = new RelayCommand(_ => ToggleBestOnly());
        }

        private void Monitor_NewResult(object? sender, SpeedtestResult r)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Results.Add(r);
                NewResultArrived?.Invoke(this, r);
            });
        }

        public void Start() => _monitor.Start();
        public void Stop() => _monitor.Stop();

        public void ToggleBestOnly()
        {
            ShowBestOnly = !ShowBestOnly;
        }

        private void RefreshResults()
        {
            // Пока логика простая — можно расширить позже
            // Сейчас просто уведомляем UI, что коллекция изменилась
            OnPropertyChanged(nameof(Results));
        }

        public void ExportCsv()
        {
            try
            {
                string path = $"results_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                using var sw = new StreamWriter(path);
                sw.WriteLine("Timestamp,Ping,Jitter,Loss,Download,Upload");

                foreach (var r in Results)
                {
                    sw.WriteLine($"{r.Timestamp:O},{r.Ping},{r.Jitter},{r.Loss},{r.Download},{r.Upload}");
                }

                MessageBox.Show($"CSV saved: {path}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error exporting CSV: " + ex.Message);
            }
        }

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
