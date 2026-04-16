using System;
using System.Windows;
using VpnSpeedAnalyzer.Logic;

namespace VpnSpeedAnalyzer
{
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Logger.Write("FATAL ERROR: " + e.ExceptionObject.ToString());
            };

            DispatcherUnhandledException += (s, e) =>
            {
                Logger.Write("UI ERROR: " + e.Exception.Message);
                e.Handled = true;
            };
        }
    }
}
