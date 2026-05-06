using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace VpnSpeedAnalyzer.Logic
{
    /// <summary>
    /// Локальный снимок сетевого контура без HTTP — меняется при переподключении VPN,
    /// смене Wi‑Fi, поднятии/опускании интерфейсов и переназначении адресов/шлюзов.
    /// </summary>
    public static class LocalNetworkFingerprint
    {
        /// <returns>Строка-снимок для сравнения; одинаковая строка ⇒ контур считается неизменным.</returns>
        public static string Compute()
        {
            var chunks = new List<string>(32);

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var props = nic.GetIPProperties();

                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    chunks.Add($"a:{nic.Id}\t{ua.Address}");
                }

                foreach (var gw in props.GatewayAddresses)
                {
                    if (gw.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    chunks.Add($"g:{nic.Id}\t{gw.Address}");
                }
            }

            chunks.Sort(StringComparer.Ordinal);
            return string.Join("|", chunks);
        }
    }
}
