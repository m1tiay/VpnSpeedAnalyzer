using System;
using System.IO;
using System.Windows;
using VpnSpeedAnalyzer.Logic;

namespace VpnSpeedAnalyzer
{
    public partial class App : Application
    {
        // Самый примитивный логгер - работает в любой ситуации
        private static void CriticalLog(string message)
        {
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "log.txt");
                var msg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  [КРИТИЧНЫЙ] {message}\n";
                File.AppendAllText(logPath, msg);
            }
            catch { }
        }
        
        public App()
        {
            CriticalLog(">>> App() конструктор НАЧАЛ выполняться");
            
            try
            {
                // КРИТИЧЕСКАЯ ДИАГНОСТИКА - если это сообщение не появилось, 
                // значит приложение крашится еще раньше, до App()
                CriticalLog("MessageBox попытка 1");
                MessageBox.Show("App() конструктор начал выполняться", "Диагностика");
                
                CriticalLog("Logger попытка 1");
                Logger.Write("=== ПРИЛОЖЕНИЕ ЗАПУСКАЕТСЯ ===");
                
                CriticalLog("Обработчики исключений установка");
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    CriticalLog("AppDomain.UnhandledException: " + e.ExceptionObject.ToString());
                    Logger.Write("КРИТИЧЕСКАЯ ОШИБКА: " + e.ExceptionObject.ToString());
                };

                DispatcherUnhandledException += (s, e) =>
                {
                    string errMsg = "ОШИБКА UI: " + e.Exception?.Message + "\n" + e.Exception?.StackTrace;
                    CriticalLog(errMsg);
                    Logger.Write(errMsg);
                    e.Handled = true;
                };
                
                CriticalLog("Обработчики исключений установлены успешно");
                Logger.Write("Обработчики исключений установлены");
                
                CriticalLog("MessageBox попытка 2");
                MessageBox.Show("App() конструктор завершился успешно", "Диагностика");
            }
            catch (Exception ex)
            {
                string errorMsg = "КРИТИЧЕСКАЯ ОШИБКА:\n" + ex.Message + "\n\n" + ex.StackTrace;
                CriticalLog("EXCEPTION в App(): " + errorMsg);
                MessageBox.Show(errorMsg, "Ошибка App()");
                // Пишем в debug если Logger не работает
                System.Diagnostics.Debug.WriteLine("Ошибка при инициализации App: " + ex.Message);
                throw;
            }
        }
    }
}
