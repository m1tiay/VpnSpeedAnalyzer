using System;
using System.Diagnostics;
using System.Windows;
using VpnSpeedAnalyzer.Logic;

namespace VpnSpeedAnalyzer
{
    /// <summary>
    /// Модальное окно с информацией о версии и заметками релиза.
    /// </summary>
    public partial class AboutWindow : Window
    {
        private const string ProjectUrl = "https://github.com/m1tiay/VpnSpeedAnalyzer";
        private const string ReleaseUrl = "https://github.com/m1tiay/VpnSpeedAnalyzer/releases/tag/v1.0.0";

        public string AppVersion { get; }
        public string ReleaseNotes { get; }

        public AboutWindow()
        {
            AppVersion = AppVersionInfo.ProductVersion;
            ReleaseNotes = ReleaseNotesProvider.GetCurrent();
            InitializeComponent();
            DataContext = this;
        }

        private void OpenRelease_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(ReleaseUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Write($"Не удалось открыть страницу релиза: {ex.Message}");
            }
        }

        private void OpenReleaseText_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenRelease_Click(sender, new RoutedEventArgs());
        }

        private void RootBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                    DragMove();
            }
            catch
            {
                // Игнорируем сбои перетаскивания окна.
            }
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(ProjectUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Write($"Не удалось открыть страницу проекта: {ex.Message}");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
