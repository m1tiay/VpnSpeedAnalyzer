using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace VpnSpeedAnalyzer
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public string CurrentIp { get; set; }
        public string CurrentCountry { get; set; }
        public string CurrentFlag { get; set; }
        public string CurrentAsn { get; set; }
        public string StatusText { get; set; } = "Idle";

        public ObservableCollection<ResultEntry> Results { get; } = new();

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ToggleBestOnlyCommand { get; }

        private readonly MonitorController _monitor;
        private readonly ResultsManager _resultsManager;

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<SpeedtestResult> NewResultArrived;

        public MainViewModel()
        {
            _resultsManager = new ResultsManager(Results);
            _monitor = new MonitorController(this, _resultsManager);

            StartCommand = new RelayCommand(_ => _monitor.Start());
            StopCommand = new RelayCommand(_ => _monitor.Stop());
            ExportCsvCommand = new RelayCommand(_ => _resultsManager.ExportCsv());
            ToggleBestOnlyCommand = new RelayCommand(_ => _resultsManager.ToggleBestOnly());
        }

        public void RaiseNewResult(SpeedtestResult r)
        {
            NewResultArrived?.Invoke(this, r);
        }

        public void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
