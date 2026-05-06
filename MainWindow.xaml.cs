using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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
        private readonly List<string> _locationTags = new();

        private ScatterPlot? _jitterPlot;
        private ScatterPlot? _pingPlot;
        private readonly List<Text> _jitterTexts = new();
        private readonly List<Text> _pingTexts = new();

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

                // Включаем темную шапку окна в Windows 10/11, чтобы не было белой полосы.
                Loaded += (_, _) => ApplyDarkTitleBar();
            }
            catch (Exception ex)
            {
                Logger.Write($"Ошибка окна: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                throw;
            }
        }

        private void InitPlots()
        {
            try
            {
                ApplyPlotTheme(JitterPlot.Plot);
                ApplyPlotTheme(PingPlot.Plot);

                // Создаём пустые графики
                _jitterPlot = JitterPlot.Plot.AddScatter(
                    xs: new double[] { 0 },
                    ys: new double[] { 0 },
                    color: Color.FromArgb(89, 217, 183),
                    lineWidth: 2f);
                _jitterPlot.MarkerSize = 0;

                _pingPlot = PingPlot.Plot.AddScatter(
                    xs: new double[] { 0 },
                    ys: new double[] { 0 },
                    color: Color.FromArgb(145, 70, 255),
                    lineWidth: 2f);
                _pingPlot.MarkerSize = 0;

                // Заголовки вынесены в XAML, поэтому на графике оставляем только данные.
                JitterPlot.Plot.XLabel(string.Empty);
                JitterPlot.Plot.YLabel(string.Empty);
                PingPlot.Plot.XLabel(string.Empty);
                PingPlot.Plot.YLabel(string.Empty);
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
        private static void ApplyPlotTheme(ScottPlot.Plot plot)
        {
            var figureBg = Color.FromArgb(29, 32, 51);
            var dataBg = Color.FromArgb(35, 39, 65);
            var grid = Color.FromArgb(58, 64, 99);
            var text = Color.FromArgb(242, 244, 255);
            var ticks = Color.FromArgb(168, 176, 217);

            plot.Style(
                figureBackground: figureBg,
                dataBackground: dataBg,
                grid: grid,
                axisLabel: text,
                tick: ticks);

            // Максимально уплотняем рабочую область графика.
            plot.Layout(left: 8, right: 2, bottom: 0, top: 0);
        }

        private static void TightenPlotLayout(ScottPlot.Plot plot)
        {
            // После AxisAuto ScottPlot может пересчитать поля.
            // Повторно применяем плотный layout перед рендером.
            plot.Layout(left: 8, right: 2, bottom: 0, top: 0);
        }

        private void ApplyDarkTitleBar()
        {
            try
            {
                const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
                int useDark = 1;
                var hwnd = new WindowInteropHelper(this).Handle;
                _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            }
            catch (Exception ex)
            {
                Logger.Write($"Не удалось включить темную шапку окна: {ex.Message}");
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private void Vm_NewResultArrived(object? sender, SpeedtestResult r)
        {
            try
            {
                _jitterData.Add(r.Jitter);
                _pingData.Add(r.Ping);
                _locationTags.Add(BuildLocationTag(r.CountryCode, r.Country));

                double[] xs = Enumerable.Range(0, _jitterData.Count)
                                        .Select(i => (double)i)
                                        .ToArray();

                // Обновляем графики
                _jitterPlot?.Update(xs, _jitterData.ToArray());
                JitterPlot.Plot.AxisAuto();
                TightenPlotLayout(JitterPlot.Plot);
                RebuildPointLabels(JitterPlot.Plot, xs, _jitterData, _locationTags, _jitterTexts);
                JitterPlot.Refresh();

                _pingPlot?.Update(xs, _pingData.ToArray());
                PingPlot.Plot.AxisAuto();
                TightenPlotLayout(PingPlot.Plot);
                RebuildPointLabels(PingPlot.Plot, xs, _pingData, _locationTags, _pingTexts);
                PingPlot.Refresh();
            }
            catch (Exception ex)
            {
                Logger.Write("Ошибка при получении результата: " + ex.ToString());
            }
        }

        private static string BuildLocationTag(string countryCode, string country)
        {
            if (!string.IsNullOrWhiteSpace(countryCode))
            {
                var code = countryCode.Trim().ToUpperInvariant();
                if (code.Length >= 2)
                    return code.Substring(0, 2);
            }

            if (string.IsNullOrWhiteSpace(country))
                return "--";
            var compact = new string(country.Where(char.IsLetter).ToArray());
            if (compact.Length >= 2)
                return compact.Substring(0, 2).ToUpperInvariant();

            return country.Trim().Substring(0, 1).ToUpperInvariant();
        }

        private static void RebuildPointLabels(
            ScottPlot.Plot plot,
            IReadOnlyList<double> xs,
            IReadOnlyList<double> ys,
            IReadOnlyList<string> tags,
            List<Text> cache)
        {
            foreach (var t in cache)
                plot.Remove(t);
            cache.Clear();

            for (var i = 0; i < xs.Count && i < ys.Count && i < tags.Count; i++)
            {
                var text = plot.AddText(tags[i], xs[i], ys[i] + 0.02, color: Color.FromArgb(168, 176, 217));
                text.FontSize = 8;
                cache.Add(text);
            }
        }
    }
}
