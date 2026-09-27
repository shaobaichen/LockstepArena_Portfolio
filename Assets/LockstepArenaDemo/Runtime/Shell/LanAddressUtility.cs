#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LockstepArena.Demo
{
    public static class LanAddressUtility
    {
        public static string? FindPreferredPrivateIpv4()
        {
            IEnumerable<IPAddress> addresses = NetworkInterface.GetAllNetworkInterfaces()
                .Where(network =>
                    network.OperationalStatus == OperationalStatus.Up &&
                    network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(network => network.GetIPProperties().UnicastAddresses)
                .Select(address => address.Address);

            return SelectPreferredPrivateIpv4(addresses);
        }

        public static string? SelectPreferredPrivateIpv4(IEnumerable<IPAddress> addresses)
        {
            if (addresses == null)
            {
                throw new ArgumentNullException(nameof(addresses));
            }

            return addresses
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => new { Address = address, Priority = GetPrivatePriority(address) })
                .Where(candidate => candidate.Priority < int.MaxValue)
                .OrderBy(candidate => candidate.Priority)
                .ThenBy(candidate => ToSortableValue(candidate.Address))
                .Select(candidate => candidate.Address.ToString())
                .FirstOrDefault();
        }

        private static int GetPrivatePriority(IPAddress address)
        {
            byte[] bytes = address.GetAddressBytes();
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return 0;
            }

            if (bytes[0] == 10)
            {
                return 1;
            }

            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return 2;
            }

            return int.MaxValue;
        }

        private static uint ToSortableValue(IPAddress address)
        {
            byte[] bytes = address.GetAddressBytes();
            return ((uint)bytes[0] << 24) |
                   ((uint)bytes[1] << 16) |
                   ((uint)bytes[2] << 8) |
                   bytes[3];
        }
    }
}
