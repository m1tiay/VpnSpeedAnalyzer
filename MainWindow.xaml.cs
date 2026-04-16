using System;
using System.Collections.Generic;
using System.Windows;
using ScottPlot.Plottable;

namespace VpnSpeedAnalyzer
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        private readonly List<double> _jitterData = new();
        private readonly List<double> _pingData = new();

        private SignalPlot _jitterLine;
        private SignalPlot _pingLine;

        public MainWindow()
        {
            InitializeComponent();

            _vm = new MainViewModel();
            DataContext = _vm;

            InitPlots();

            _vm.NewResultArrived += Vm_NewResultArrived;
        }

        private void InitPlots()
        {
            _jitterLine = JitterPlot.Plot.AddSignal(_jitterData.ToArray());
            JitterPlot.Plot.Title("Jitter (ms)");
            JitterPlot.Plot.YLabel("ms");
            JitterPlot.Plot.XLabel("Test #");
            JitterPlot.Plot.SetAxisLimits(yMin: 0);
            JitterPlot.Refresh();

            _pingLine = PingPlot.Plot.AddSignal(_pingData.ToArray());
            PingPlot.Plot.Title("Ping (ms)");
            PingPlot.Plot.YLabel("ms");
            PingPlot.Plot.XLabel("Test #");
            PingPlot.Plot.SetAxisLimits(yMin: 0);
            PingPlot.Refresh();
        }

        private void Vm_NewResultArrived(object sender, SpeedtestResult r)
        {
            Dispatcher.Invoke(() =>
            {
                _jitterData.Add(r.Jitter);
                _pingData.Add(r.Ping);

                _jitterLine.Update(_jitterData.ToArray());
                _pingLine.Update(_pingData.ToArray());

                JitterPlot.Refresh();
                PingPlot.Refresh();
            });
        }
    }
}
