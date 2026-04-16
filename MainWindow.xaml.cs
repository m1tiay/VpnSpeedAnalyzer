using ScottPlot;
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
            _jitterPlot = WpfPlotJitter.Plot.AddScatter(
                xs: Array.Empty<double>(),
                ys: Array.Empty<double>(),
                color: System.Drawing.Color.DeepSkyBlue,
                lineWidth: 2);

            WpfPlotJitter.Plot.Title("Jitter (ms)");
            WpfPlotJitter.Plot.XLabel("Test #");
            WpfPlotJitter.Plot.YLabel("ms");
            WpfPlotJitter.Refresh();

            // Ping plot
            _pingPlot = WpfPlotPing.Plot.AddScatter(
                xs: Array.Empty<double>(),
                ys: Array.Empty<double>(),
                color: System.Drawing.Color.OrangeRed,
                lineWidth: 2);

            WpfPlotPing.Plot.Title("Ping (ms)");
            WpfPlotPing.Plot.XLabel("Test #");
            WpfPlotPing.Plot.YLabel("ms");
            WpfPlotPing.Refresh();
        }

        private void Vm_NewResultArrived(object sender, SpeedtestResult r)
        {
            Dispatcher.Invoke(() =>
            {
                // Add new data
                _jitterData.Add(r.Jitter);
                _pingData.Add(r.Ping);

                // X axis = test index
                double[] xs = Enumerable.Range(0, _jitterData.Count).Select(i => (double)i).ToArray();

                // Update jitter plot
                _jitterPlot.Update(xs, _jitterData.ToArray());
                WpfPlotJitter.Plot.AxisAuto();
                WpfPlotJitter.Refresh();

                // Update ping plot
                _pingPlot.Update(xs, _pingData.ToArray());
                WpfPlotPing.Plot.AxisAuto();
                WpfPlotPing.Refresh();
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
