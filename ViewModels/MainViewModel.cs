using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using VpnSpeedAnalyzer.Logic;
using VpnSpeedAnalyzer.Models;

namespace VpnSpeedAnalyzer
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<SpeedtestResult>? NewResultArrived;

        private readonly MonitorController _monitor;
        private readonly ResultsManager _resultsManager;

        public ObservableCollection<ResultEntry> Results { get; } = new();

        public MainViewModel()
        {
            _monitor = new MonitorController(this);
            _monitor.NewResult += Monitor_NewResult;

            _resultsManager = new ResultsManager(Results);
        }

        private void Monitor_NewResult(object? sender, SpeedtestResult r)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
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

                NewResultArrived?.Invoke(this, r);
            });
        }

        public void Start() => _monitor.Start();
        public void Stop() => _monitor.Stop();

        public void ToggleBestOnly() => _resultsManager.ApplyBestOnly();

        public void ExportCsv()
        {
            try
            {
                string path = $"results_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                using var sw = new StreamWriter(path);
                sw.WriteLine("Timestamp,IP,Country,Ping,Jitter,Loss,Download,Upload");

                foreach (var r in Results)
                {
                    sw.WriteLine($"{r.Timestamp},{r.Ip},{r.Country},{r.Ping},{r.Jitter},{r.Loss},{r.Download},{r.Upload}");
                }

                MessageBox.Show($"CSV saved: {path}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error exporting CSV: " + ex.Message);
            }
        }

        public void Notify(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
