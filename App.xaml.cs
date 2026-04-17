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
                
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    Logger.Write("КРИТИЧЕСКАЯ ОШИБКА: " + e.ExceptionObject.ToString());
                };

                DispatcherUnhandledException += (s, e) =>
                {
                    Logger.Write("ОШИБКА UI: " + e.Exception.Message + "\n" + e.Exception.StackTrace);
                    e.Handled = true;
                };
                
                Logger.Write("Обработчики исключений установлены");
            }
            catch (Exception ex)
            {
                // Пишем в debug если Logger не работает
                System.Diagnostics.Debug.WriteLine("Ошибка при инициализации App: " + ex.Message);
                throw;
            }
        }
    }
}
