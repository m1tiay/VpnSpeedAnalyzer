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

            Log.Info("Monitor START");
            _ = Loop();
        }

        public void Stop()
        {
            _running = false;
            _vm.StatusText = "Stopped";
            _vm.OnPropertyChanged(nameof(_vm.StatusText));

            Log.Info("Monitor STOP");
        }

        private async Task Loop()
        {
            Log.Info("Monitor loop started");

            while (_running)
            {
                try
                {
                    Log.Info($"Loop tick, state={_state}");

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
                            Log.Info("Cooldown state, waiting 30s");
                            await Task.Delay(30000);
                            _state = MonitorState.FastCheck;
                            Log.Info("Switching to FastCheck");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Exception in Loop: {ex}");
                }
            }

            Log.Info("Monitor loop stopped");
        }

        private async Task CheckIpChange()
        {
            Log.Info("Checking IP...");

            IpInfo info = null;
            try
            {
                info = await _ipService.GetCurrentAsync();
            }
            catch (Exception ex)
            {
                Log.Error($"GetCurrentAsync failed: {ex}");
                return;
            }

            if (info == null)
            {
                Log.Error("GetCurrentAsync returned null");
                return;
            }

            Log.Info($"Current IP: {info.Ip}, Country={info.CountryName}, ASN={info.Asn}");

            UpdateVmIp(info);

            if (_lastIp == null)
            {
                _lastIp = info.Ip;
                Log.Info($"Initial IP set: {_lastIp}");
                return;
            }

            if (info.Ip != _lastIp)
            {
                Log.Info($"IP changed: {_lastIp} -> {info.Ip}");
                _lastIp = info.Ip;
                await OnHostChanged(info);
            }
            else
            {
                Log.Info("IP not changed");
            }
        }

        private async Task OnHostChanged(IpInfo info)
        {
            _state = MonitorState.Testing;
            _vm.StatusText = "Host changed → Running Speedtest...";
            _vm.OnPropertyChanged(nameof(_vm.StatusText));

            Log.Info("Host changed → running speedtest");

            SpeedtestResult result = null;
            try
            {
                result = await _speedtest.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Error($"Speedtest.RunAsync failed: {ex}");
            }

            if (result != null)
            {
                Log.Info($"Speedtest result: ping={result.Ping}, jitter={result.Jitter}, dl={result.Download}, ul={result.Upload}");
                _results.Add(result, info);
                _vm.RaiseNewResult(result);
            }
            else
            {
                Log.Error("Speedtest returned null result");
            }

            _state = MonitorState.Cooldown;
            _vm.StatusText = "Cooldown 30s...";
            _vm.OnPropertyChanged(nameof(_vm.StatusText));

            Log.Info("Entering Cooldown state");
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

            Log.Info($"VM updated: IP={info.Ip}, Country={info.CountryName}, ASN={info.Asn}, Flag={_vm.CurrentFlag}");
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
