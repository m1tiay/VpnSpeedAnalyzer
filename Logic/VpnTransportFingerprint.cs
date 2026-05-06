using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace VpnSpeedAnalyzer.Logic
{
    /// <summary>
    /// Отпечаток транспортных VPN-соединений:
    /// ищем TCP-сессии ESTABLISHED, у которых локальный IP принадлежит туннельному интерфейсу.
    /// </summary>
    public static class VpnTransportFingerprint
    {
        private const int AfInet = 2;
        private const int TcpTableOwnerPidAll = 5;
        private const uint ErrorInsufficientBuffer = 122;
        private const uint TcpStateEstablished = 5;

        public sealed class TopProcessSnapshot
        {
            public int ProcessId { get; init; }
            public int ConnectionCount { get; init; }
            public string Fingerprint { get; init; } = string.Empty;
        }

        public static TopProcessSnapshot? GetTopProcessSnapshot(int preferredProcessId = 0)
        {
            try
            {
                var tunnelIps = CollectTunnelIpv4Addresses();
                if (tunnelIps.Count == 0)
                    return null;

                var rows = GetTcpRowsOwnerPid();
                var tunnelRows = rows
                    .Where(r => r.State == TcpStateEstablished)
                    .Where(r => tunnelIps.Contains(r.LocalAddress))
                    .Where(r => r.RemoteAddress.AddressFamily == AddressFamily.InterNetwork)
                    .Where(r => !IPAddress.IsLoopback(r.RemoteAddress))
                    .ToList();

                if (tunnelRows.Count == 0)
                    return null;

                var targetPid = preferredProcessId > 0
                                && tunnelRows.Any(r => r.ProcessId == preferredProcessId)
                    ? preferredProcessId
                    : tunnelRows.GroupBy(r => r.ProcessId)
                        .OrderByDescending(g => g.Count())
                        .ThenBy(g => g.Key)
                        .First().Key;

                var forPid = tunnelRows.Where(r => r.ProcessId == targetPid).ToList();
                if (forPid.Count == 0)
                    return null;

                var endpoints = forPid
                    .Select(r => $"{r.RemoteAddress}:{r.RemotePort}")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList();

                return new TopProcessSnapshot
                {
                    ProcessId = targetPid,
                    ConnectionCount = forPid.Count,
                    Fingerprint = string.Join("|", endpoints)
                };
            }
            catch (Exception ex)
            {
                Logger.Write($"VPN fingerprint error: {ex.GetType().Name}: {ex.Message}");
                return null;
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

        private static List<TcpRowOwnerPid> GetTcpRowsOwnerPid()
        {
            var result = new List<TcpRowOwnerPid>(128);
            var size = 0;
            var ret = GetExtendedTcpTable(IntPtr.Zero, ref size, true, AfInet, TcpTableOwnerPidAll, 0);
            if (ret != ErrorInsufficientBuffer || size <= 0)
                return result;

            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                ret = GetExtendedTcpTable(ptr, ref size, true, AfInet, TcpTableOwnerPidAll, 0);
                if (ret != 0)
                    return result;

                var numEntries = Marshal.ReadInt32(ptr);
                var rowPtr = IntPtr.Add(ptr, 4);
                var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();

                for (var i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPtr);
                    rowPtr = IntPtr.Add(rowPtr, rowSize);

                    var localAddress = new IPAddress(row.LocalAddr);
                    var remoteAddress = new IPAddress(row.RemoteAddr);
                    var localPort = ConvertPort(row.LocalPort);
                    var remotePort = ConvertPort(row.RemotePort);

                    result.Add(new TcpRowOwnerPid
                    {
                        State = row.State,
                        LocalAddress = localAddress,
                        LocalPort = localPort,
                        RemoteAddress = remoteAddress,
                        RemotePort = remotePort,
                        ProcessId = (int)row.OwningPid
                    });
                }

                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
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

        private static int ConvertPort(uint rawPort)
        {
            // В MIB_TCPROW_OWNER_PID порт хранится как 4 байта, где значимы первые 2 байта в network order.
            // При маршалинге в uint и little-endian shift-арифметика легко даёт неверные значения.
            // Берём байты напрямую и собираем порт так же, как в PowerShell/WinAPI-примерах.
            var bytes = BitConverter.GetBytes(rawPort);
            return (bytes[0] << 8) | bytes[1];
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int dwOutBufLen,
            bool sort,
            int ipVersion,
            int tblClass,
            uint reserved);

        [StructLayout(LayoutKind.Sequential)]
        private struct MibTcpRowOwnerPid
        {
            public uint State;
            public uint LocalAddr;
            public uint LocalPort;
            public uint RemoteAddr;
            public uint RemotePort;
            public uint OwningPid;
        }

        private sealed class TcpRowOwnerPid
        {
            public uint State { get; init; }
            public IPAddress LocalAddress { get; init; } = IPAddress.None;
            public int LocalPort { get; init; }
            public IPAddress RemoteAddress { get; init; } = IPAddress.None;
            public int RemotePort { get; init; }
            public int ProcessId { get; init; }
        }
    }
}
