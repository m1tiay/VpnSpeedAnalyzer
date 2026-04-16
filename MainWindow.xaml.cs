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

        private ScatterPlot _jitterPlot;
        private ScatterPlot _pingPlot;

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
            // Jitter plot
            _jitterPlot = PlotJitter.Plot.AddScatter(
                xs: Array.Empty<double>(),
                ys: Array.Empty<double>(),
                color: System.Drawing.Color.DeepSkyBlue,
                lineWidth: 2);

            PlotJitter.Plot.Title("Jitter (ms)");
            PlotJitter.Plot.XLabel("Test #");
            PlotJitter.Plot.YLabel("ms");
            PlotJitter.Refresh();

            // Ping plot
            _pingPlot = PlotPing.Plot.AddScatter(
                xs: Array.Empty<double>(),
                ys: Array.Empty<double>(),
                color: System.Drawing.Color.OrangeRed,
                lineWidth: 2);

            PlotPing.Plot.Title("Ping (ms)");
            PlotPing.Plot.XLabel("Test #");
            PlotPing.Plot.YLabel("ms");
            PlotPing.Refresh();
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

                _jitterPlot.Update(xs, _jitterData.ToArray());
                PlotJitter.Plot.AxisAuto();
                PlotJitter.Refresh();

                _pingPlot.Update(xs, _pingData.ToArray());
                PlotPing.Plot.AxisAuto();
                PlotPing.Refresh();
            });
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            _vm.Start();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _vm.Stop();
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            _vm.ExportCsv();
        }

        private void ShowBestOnly_Click(object sender, RoutedEventArgs e)
        {
            _vm.ToggleBestOnly();
        }
    }
}
