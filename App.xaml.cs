using System;
using System.Windows;
using VpnSpeedAnalyzer.Logic;

namespace VpnSpeedAnalyzer
{
    public partial class App : Application
    {
        public App()
        {
            try
            {
                Logger.Write("=== ПРИЛОЖЕНИЕ ЗАПУСКАЕТСЯ ===");
                Logger.Write($"Версия приложения: {AppVersionInfo.InformationalVersion}");
                ReleaseStateService.HandleStartup();

                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    Logger.Write("КРИТИЧЕСКАЯ ОШИБКА: " + e.ExceptionObject);
                };

                DispatcherUnhandledException += (s, e) =>
                {
                    Logger.Write($"ОШИБКА UI: {e.Exception?.Message}{Environment.NewLine}{e.Exception?.StackTrace}");
                    e.Handled = true;
                };

                Logger.Write("Обработчики исключений установлены");
            }
            catch (Exception ex)
            {
                Logger.Write($"Ошибка при инициализации App: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
                MessageBox.Show($"Ошибка App(): {ex.Message}", "VPN Speed Analyzer");
                throw;
            }
        }
    }
}
