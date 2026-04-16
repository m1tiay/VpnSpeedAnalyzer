using System;
using System.Threading;
using System.Threading.Tasks;
using VpnSpeedAnalyzer.Models;
using VpnSpeedAnalyzer.Services;

namespace VpnSpeedAnalyzer.Logic
{
    public class MonitorController
    {
        private readonly MainViewModel _vm;
        private readonly IpInfoService _ipService = new();
        private readonly SpeedtestService _speedtest = new();

        private CancellationTokenSource? _cts;
        private IpInfo? _lastIp;

        public event EventHandler<SpeedtestResult>? NewResult;

        public MonitorController(MainViewModel vm)
        {
            _vm = vm;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => Loop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        private async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Logger.Write("Checking IP...");
                var info = await _ipService.GetCurrentAsync();
                Logger.Write("IP result: " + (info?.Ip ?? "NULL"));


                if (info != null)
                {
                    if (_lastIp == null || info.Ip != _lastIp.Ip)
                    {
                        Logger.Write("Running speedtest...");
                        var result = await _speedtest.RunAsync();
                        Logger.Write("Speedtest result: " + (result == null ? "NULL" : "OK"));

                        if (result != null)
                            NewResult?.Invoke(this, result);
                    }

                    _lastIp = info;
                }

                await Task.Delay(15000, token);
            }
        }
    }
}
