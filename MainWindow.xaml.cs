using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VpnSpeedAnalyzer.Logic;
using VpnSpeedAnalyzer.Models;

namespace VpnSpeedAnalyzer
{
    /// <summary>
    /// Основное окно приложения
    /// </summary>
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
                Logger.Write("Инициализация окна START");

                InitializeComponent();
                Logger.Write("InitializeComponent ОК");

                _vm = new MainViewModel();
                Logger.Write("ViewModel ОК");

                DataContext = _vm;
                Logger.Write("DataContext установлен");

                _vm.NewResultArrived += Vm_NewResultArrived;
                Logger.Write("Событие подписано");

                InitPlots();
                Logger.Write("InitPlots ОК");
            }
            catch (Exception ex)
            {
                Logger.Write("Ошибка окна: " + ex.ToString());
                throw;
            }
        }

        private void InitPlots()
        {
            try
            {
                // Создаём пустые графики
                _jitterPlot = JitterPlot.Plot.AddScatter(
                    xs: new double[] { 0 },
                    ys: new double[] { 0 },
                    color: System.Drawing.Color.DeepSkyBlue,
                    lineWidth: 2);

                _pingPlot = PingPlot.Plot.AddScatter(
                    xs: new double[] { 0 },
                    ys: new double[] { 0 },
                    color: System.Drawing.Color.OrangeRed,
                    lineWidth: 2);

                JitterPlot.Plot.Title("Дрожание (мс)");
                JitterPlot.Plot.XLabel("Номер теста");
                JitterPlot.Plot.YLabel("мс");

                PingPlot.Plot.Title("Пинг (мс)");
                PingPlot.Plot.XLabel("Номер теста");
                PingPlot.Plot.YLabel("мс");
            }
            catch (Exception ex)
            {
                Logger.Write("Ошибка инициализации графиков: " + ex.ToString());
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

                    // Обновляем графики
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
                Logger.Write("Ошибка при получении результата: " + ex.ToString());
            }
        }
    }
}
