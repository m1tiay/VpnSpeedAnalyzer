using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace VpnSpeedAnalyzer.Logic
{
    /// <summary>
    /// Отпечаток транспортных VPN-соединений:
    /// ищем TCP-сессии ESTABLISHED, у которых локальный IP принадлежит туннельному интерфейсу.
    /// </summary>
    public static class VpnTransportFingerprint
    {
        public static string Compute()
        {
            try
            {
                var tunnelIps = CollectTunnelIpv4Addresses();
                if (tunnelIps.Count == 0)
                    return string.Empty;

                var entries = new List<string>(16);
                foreach (var c in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections())
                {
                    if (c.State != TcpState.Established)
                        continue;

                    if (c.LocalEndPoint.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    if (!tunnelIps.Contains(c.LocalEndPoint.Address))
                        continue;

                    var remote = c.RemoteEndPoint.Address;
                    if (remote.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    if (IPAddress.IsLoopback(remote))
                        continue;

                    entries.Add($"{remote}:{c.RemoteEndPoint.Port}");
                }

                if (entries.Count == 0)
                    return string.Empty;

                entries.Sort(StringComparer.Ordinal);
                return string.Join("|", entries.Distinct(StringComparer.Ordinal));
            }
            catch (Exception ex)
            {
                Logger.Write($"VPN fingerprint error: {ex.GetType().Name}: {ex.Message}");
                return string.Empty;
            }
        }

        private static HashSet<IPAddress> CollectTunnelIpv4Addresses()
        {
            var result = new HashSet<IPAddress>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (!LooksLikeVpnInterface(nic))
                    continue;

                var props = nic.GetIPProperties();
                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    result.Add(ua.Address);
                }
            }

            return result;
        }

        private static bool LooksLikeVpnInterface(NetworkInterface nic)
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                return true;

            var text = $"{nic.Name} {nic.Description}".ToLowerInvariant();
            return text.Contains("wintun")
                   || text.Contains("wireguard")
                   || text.Contains("openvpn")
                   || text.Contains("tap")
                   || text.Contains("tun")
                   || text.Contains("vpn");
        }
    }
}
