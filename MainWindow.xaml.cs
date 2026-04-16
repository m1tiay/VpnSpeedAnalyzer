using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VpnSpeedAnalyzer.Logic;
using VpnSpeedAnalyzer.Models;

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
            try
            {
                Logger.Write("MainWindow ctor START");

                InitializeComponent();
                Logger.Write("InitializeComponent OK");

                _vm = new MainViewModel();
                Logger.Write("MainViewModel OK");

                DataContext = _vm;
                Logger.Write("DataContext set");

                _vm.NewResultArrived += Vm_NewResultArrived;
                Logger.Write("Event subscribed");

                InitPlots();
                Logger.Write("InitPlots OK");
            }
            catch (Exception ex)
            {
                Logger.Write("MainWindow ERROR: " + ex.ToString());
                throw;
            }
        }

        private void InitPlots()
        {
            try
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
            catch (Exception ex)
            {
                Logger.Write("InitPlots ERROR: " + ex.ToString());
                throw;
            }
        }

        private void Vm_NewResultArrived(object? sender, SpeedtestResult r)
        {
            try
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
            catch (Exception ex)
            {
                Logger.Write("Vm_NewResultArrived ERROR: " + ex.ToString());
                throw;
            }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            Logger.Write("Start_Click");
            _vm.Start();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            Logger.Write("Stop_Click");
            _vm.Stop();
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            Logger.Write("ExportCsv_Click");
            _vm.ExportCsv();
        }

        private void ShowBestOnly_Click(object sender, RoutedEventArgs e)
        {
            Logger.Write("ShowBestOnly_Click");
            _vm.ToggleBestOnly();
        }
    }
}
