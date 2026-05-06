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
    /// сначала ищем TCP-сессии ESTABLISHED на туннельных IP, при их отсутствии —
    /// резервно берём вероятные VPN-транспорты по характерным портам.
    /// </summary>
    public static class VpnTransportFingerprint
    {
        private static readonly int[] KnownVpnPorts =
            { 443, 1194, 1443, 51820, 51821, 8443, 9443 };

        public static string Compute()
        {
            try
            {
                var tunnelIps = CollectTunnelIpv4Addresses();
                var tcp = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();

                var entries = CollectByTunnelLocalIp(tcp, tunnelIps);
                if (entries.Count == 0)
                    entries = CollectByKnownVpnPorts(tcp);

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

        private static List<string> CollectByTunnelLocalIp(TcpConnectionInformation[] tcp, HashSet<IPAddress> tunnelIps)
        {
            if (tunnelIps.Count == 0)
                return new List<string>();

            var entries = new List<string>(16);
            foreach (var c in tcp)
            {
                if (c.State != TcpState.Established)
                    continue;

                if (c.LocalEndPoint.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                if (!tunnelIps.Contains(c.LocalEndPoint.Address))
                    continue;

                var remote = c.RemoteEndPoint.Address;
                if (!IsGoodRemoteIpv4(remote))
                    continue;

                entries.Add($"{remote}:{c.RemoteEndPoint.Port}");
            }

            return entries;
        }

        private static List<string> CollectByKnownVpnPorts(TcpConnectionInformation[] tcp)
        {
            var entries = new List<string>(8);
            foreach (var c in tcp)
            {
                if (c.State != TcpState.Established)
                    continue;

                if (c.RemoteEndPoint.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                if (!KnownVpnPorts.Contains(c.RemoteEndPoint.Port))
                    continue;

                var remote = c.RemoteEndPoint.Address;
                if (!IsGoodRemoteIpv4(remote))
                    continue;

                entries.Add($"{remote}:{c.RemoteEndPoint.Port}");
            }

            return entries;
        }

        private static bool IsGoodRemoteIpv4(IPAddress remote)
        {
            if (remote.AddressFamily != AddressFamily.InterNetwork)
                return false;
            if (IPAddress.IsLoopback(remote))
                return false;
            if (IsPrivateOrSpecial(remote))
                return false;
            return true;
        }

        private static bool IsPrivateOrSpecial(IPAddress ip)
        {
            var b = ip.GetAddressBytes();
            // 10.0.0.0/8
            if (b[0] == 10) return true;
            // 172.16.0.0/12
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
            // 192.168.0.0/16
            if (b[0] == 192 && b[1] == 168) return true;
            // CGNAT 100.64.0.0/10
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return true;
            // Link-local 169.254.0.0/16
            if (b[0] == 169 && b[1] == 254) return true;
            // Benchmark/testing 198.18.0.0/15
            if (b[0] == 198 && (b[1] == 18 || b[1] == 19)) return true;
            return false;
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
