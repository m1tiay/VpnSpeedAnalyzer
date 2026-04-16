using System;
using System.Threading;
using System.Threading.Tasks;

namespace VpnSpeedAnalyzer
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
                var info = await _ipService.GetCurrentAsync();

                if (info != null)
                {
                    if (_lastIp == null || info.Ip != _lastIp.Ip)
                    {
                        var result = await _speedtest.RunAsync();
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
