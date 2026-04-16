using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace VpnSpeedAnalyzer
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        private readonly List<double> _jitterData = new();
        private readonly List<double> _pingData = new();

        private ScatterPlot? _jitterPlot;
        private ScatterPlot? _pingPlot;

        public MainWindow()
        {
            InitializeComponent();

            _vm = new MainViewModel();
            DataContext = _vm;

            _vm.NewResultArrived += Vm_NewResultArrived;

            InitPlots();
        }

        private void InitPlots()
        {
            _jitterPlot = JitterPlot.Plot.AddScatter(
                xs: Array.Empty<double>(),
                ys: Array.Empty<double>(),
                color: System.Drawing.Color.DeepSkyBlue,
                lineWidth: 2);

            JitterPlot.Plot.Title("Jitter (ms)");
            JitterPlot.Plot.XLabel("Test #");
            JitterPlot.Plot.YLabel("ms");
            JitterPlot.Refresh();

            _pingPlot = PingPlot.Plot.AddScatter(
                xs: Array.Empty<double>(),
                ys: Array.Empty<double>(),
                color: System.Drawing.Color.OrangeRed,
                lineWidth: 2);

            PingPlot.Plot.Title("Ping (ms)");
            PingPlot.Plot.XLabel("Test #");
            PingPlot.Plot.YLabel("ms");
            PingPlot.Refresh();
        }

        private void Vm_NewResultArrived(object? sender, SpeedtestResult r)
        {
            Dispatcher.Invoke(() =>
            {
                _jitterData.Add(r.Jitter);
                _pingData.Add(r.Ping);

                double[] xs = Enumerable.Range(0, _jitterData.Count)
                                        .Select(i => (double)i)
                                        .ToArray();

                _jitterPlot?.Update(xs, _jitterData.ToArray());
                JitterPlot.Plot.AxisAuto();
                JitterPlot.Refresh();

                _pingPlot?.Update(xs, _pingData.ToArray());
                PingPlot.Plot.AxisAuto();
                PingPlot.Refresh();
            });
        }

        private void Start_Click(object sender, RoutedEventArgs e) => _vm.Start();
        private void Stop_Click(object sender, RoutedEventArgs e) => _vm.Stop();
        private void ExportCsv_Click(object sender, RoutedEventArgs e) => _vm.ExportCsv();
        private void ShowBestOnly_Click(object sender, RoutedEventArgs e) => _vm.ToggleBestOnly();
    }
}
