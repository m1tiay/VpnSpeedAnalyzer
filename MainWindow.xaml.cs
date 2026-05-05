using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
        // Самый примитивный логгер
        private static void CriticalLog(string message)
        {
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "log.txt");
                var msg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  [MainWindow] {message}\n";
                File.AppendAllText(logPath, msg);
            }
            catch { }
        }

        private readonly MainViewModel _vm;

        private readonly List<double> _jitterData = new();
        private readonly List<double> _pingData = new();

        private ScatterPlot? _jitterPlot;
        private ScatterPlot? _pingPlot;

        public MainWindow()
        {
            CriticalLog(">>> MainWindow конструктор НАЧАЛ");
            try
            {
                CriticalLog("InitializeComponent попытка");
                Logger.Write("Инициализация окна START");

                InitializeComponent();
                CriticalLog("InitializeComponent ОК");
                Logger.Write("InitializeComponent ОК");

                CriticalLog("MainViewModel создание попытка");
                _vm = new MainViewModel();
                CriticalLog("MainViewModel создание ОК");
                Logger.Write("ViewModel ОК");

                CriticalLog("DataContext установка");
                DataContext = _vm;
                CriticalLog("DataContext установлен");
                Logger.Write("DataContext установлен");

                CriticalLog("Событие NewResultArrived подписка");
                _vm.NewResultArrived += Vm_NewResultArrived;
                CriticalLog("Событие подписано");
                Logger.Write("Событие подписано");

                CriticalLog("InitPlots попытка");
                InitPlots();
                CriticalLog("InitPlots ОК");
                Logger.Write("InitPlots ОК");
                
                CriticalLog(">>> MainWindow конструктор ЗАВЕРШИЛСЯ УСПЕШНО");
            }
            catch (Exception ex)
            {
                string errMsg = "MainWindow ОШИБКА: " + ex.GetType().Name + " | " + ex.Message + " | " + ex.StackTrace;
                CriticalLog(errMsg);
                Logger.Write("Ошибка окна: " + ex.ToString());
                throw;
            }
        }

        private void InitPlots()
        {
            try
            {
                ApplyPlotTheme(JitterPlot);
                ApplyPlotTheme(PingPlot);

                // Создаём пустые графики
                _jitterPlot = JitterPlot.Plot.AddScatter(
                    xs: new double[] { 0 },
                    ys: new double[] { 0 },
                    color: Color.FromArgb(89, 217, 183),
                    lineWidth: 2.5);
                _jitterPlot.MarkerSize = 0;

                _pingPlot = PingPlot.Plot.AddScatter(
                    xs: new double[] { 0 },
                    ys: new double[] { 0 },
                    color: Color.FromArgb(145, 70, 255),
                    lineWidth: 2.5);
                _pingPlot.MarkerSize = 0;

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

        /// <summary>
        /// Единая визуальная тема графиков в стиле приложения
        /// </summary>
        private static void ApplyPlotTheme(ScottPlot.WPF.WpfPlot plotControl)
        {
            var figureBg = Color.FromArgb(29, 32, 51);
            var dataBg = Color.FromArgb(35, 39, 65);
            var grid = Color.FromArgb(58, 64, 99);
            var text = Color.FromArgb(242, 244, 255);
            var ticks = Color.FromArgb(168, 176, 217);

            plotControl.Plot.Style(
                figureBackground: figureBg,
                dataBackground: dataBg,
                grid: grid,
                axisLabel: text,
                tick: ticks,
                title: text);
        }

        private void Vm_NewResultArrived(object? sender, SpeedtestResult r)
        {
            try
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
            }
            catch (Exception ex)
            {
                Logger.Write("Ошибка при получении результата: " + ex.ToString());
            }
        }
    }
}
