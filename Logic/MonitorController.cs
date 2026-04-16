using System;
using System.Threading.Tasks;

namespace VpnSpeedAnalyzer
{
    public class MonitorController
    {
        private readonly MainViewModel _vm;
        private readonly ResultsManager _results;

        private readonly IpInfoService _ipService = new();
        private readonly SpeedtestService _speedtest = new();

        private bool _running = false;
        private string _lastIp = null;

        private enum MonitorState
        {
            NormalCheck,
            Testing,
            Cooldown,
            FastCheck
        }

        private MonitorState _state = MonitorState.NormalCheck;

        public MonitorController(MainViewModel vm, ResultsManager results)
        {
            _vm = vm;
            _results = results;
        }

        public void Start()
        {
            if (_running)
                return;

            _running = true;
            _vm.StatusText = "Monitoring...";
            _vm.OnPropertyChanged(nameof(_vm.StatusText));

            _ = Loop();
        }

        public void Stop()
        {
            _running = false;
            _vm.StatusText = "Stopped";
            _vm.OnPropertyChanged(nameof(_vm.StatusText));
        }

        private async Task Loop()
        {
            while (_running)
            {
                try
                {
                    switch (_state)
                    {
                        case MonitorState.NormalCheck:
                            await CheckIpChange();
                            await Task.Delay(15000);
                            break;

                        case MonitorState.FastCheck:
                            await CheckIpChange();
                            await Task.Delay(5000);
                            break;

                        case MonitorState.Cooldown:
                            await Task.Delay(30000);
                            _state = MonitorState.FastCheck;
                            break;
                    }
                }
                catch
                {
                    // swallow errors to keep loop alive
                }
            }
        }

        private async Task CheckIpChange()
        {
            var info = await _ipService.GetCurrentAsync();
            if (info == null)
                return;

            UpdateVmIp(info);

            if (_lastIp == null)
            {
                _lastIp = info.Ip;
                return;
            }

            if (info.Ip != _lastIp)
            {
                _lastIp = info.Ip;
                await OnHostChanged(info);
            }
        }

        private async Task OnHostChanged(IpInfo info)
        {
            _state = MonitorState.Testing;
            _vm.StatusText = "Host changed → Running Speedtest...";
            _vm.OnPropertyChanged(nameof(_vm.StatusText));

            var result = await _speedtest.RunAsync();
            if (result != null)
            {
                _results.Add(result, info);
                _vm.RaiseNewResult(result);
            }

            _state = MonitorState.Cooldown;
            _vm.StatusText = "Cooldown 30s...";
            _vm.OnPropertyChanged(nameof(_vm.StatusText));
        }

        private void UpdateVmIp(IpInfo info)
        {
            _vm.CurrentIp = info.Ip;
            _vm.CurrentCountry = info.CountryName;
            _vm.CurrentAsn = info.Asn;
            _vm.CurrentFlag = CountryCodeToFlag(info.CountryCode);

            _vm.OnPropertyChanged(nameof(_vm.CurrentIp));
            _vm.OnPropertyChanged(nameof(_vm.CurrentCountry));
            _vm.OnPropertyChanged(nameof(_vm.CurrentAsn));
            _vm.OnPropertyChanged(nameof(_vm.CurrentFlag));
        }

        private string CountryCodeToFlag(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length != 2)
                return "";

            return char.ConvertFromUtf32(code[0] + 0x1F1A5) +
                   char.ConvertFromUtf32(code[1] + 0x1F1A5);
        }
    }
}
