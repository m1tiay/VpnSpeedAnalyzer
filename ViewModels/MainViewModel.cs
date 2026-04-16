using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace VpnSpeedAnalyzer
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<SpeedtestResult>? NewResultArrived;

        private readonly MonitorController _monitor;

        public ObservableCollection<SpeedtestResult> Results { get; } = new();

        public MainViewModel()
        {
            _monitor = new MonitorController(this);
            _monitor.NewResult += Monitor_NewResult;
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

        public void ToggleBestOnly() { }

        public void ExportCsv()
        {
            try
            {
                string path = $"results_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                using var sw = new StreamWriter(path);
                sw.WriteLine("Timestamp,Ping,Jitter,Loss,Download,Upload");

                foreach (var r in Results)
                    sw.WriteLine($"{r.Timestamp:O},{r.Ping},{r.Jitter},{r.Loss},{r.Download},{r.Upload}");

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
