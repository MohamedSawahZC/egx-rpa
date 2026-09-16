// =============================================================================
//  DeviceLock.cs - bind this EXE to one operator PC via NIC MAC address.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace RbaCore
{
    public static class DeviceLock
    {
        // Allowed operator PCs (normalized hex, no separators).
        private static readonly string[] AllowedMacs = new string[]
        {
            "D0460CA0DC00", // trdr1095cod1
            "EC63D7C74052", // current Wi-Fi (Intel AX201)
            "04421A87BA6B"  // current Ethernet (Realtek)
        };

        public static bool IsThisMachineAllowed()
        {
            List<string> here = CollectNormalizedMacs();
            for (int i = 0; i < here.Count; i++)
            {
                for (int a = 0; a < AllowedMacs.Length; a++)
                {
                    if (string.Equals(here[i], AllowedMacs[a], StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static string DescribeLocalMacs()
        {
            List<string> here = CollectNormalizedMacs();
            if (here.Count == 0) { return "(none)"; }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < here.Count; i++)
            {
                if (i > 0) { sb.Append(", "); }
                sb.Append(FormatMac(here[i]));
            }
            return sb.ToString();
        }

        private static List<string> CollectNormalizedMacs()
        {
            List<string> list = new List<string>();
            NetworkInterface[] nics;
            try
            {
                nics = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch
            {
                return list;
            }
            if (nics == null) { return list; }

            for (int i = 0; i < nics.Length; i++)
            {
                NetworkInterface nic = nics[i];
                if (nic == null) { continue; }
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) { continue; }

                string mac = NormalizeMac(PhysicalToHex(nic.GetPhysicalAddress()));
                if (mac.Length != 12) { continue; }
                if (mac == "000000000000") { continue; }
                if (!list.Contains(mac)) { list.Add(mac); }
            }
            return list;
        }

        private static string PhysicalToHex(PhysicalAddress address)
        {
            if (address == null) { return ""; }
            byte[] bytes;
            try { bytes = address.GetAddressBytes(); }
            catch { return ""; }
            if (bytes == null || bytes.Length == 0) { return ""; }

            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }

        private static string NormalizeMac(string value)
        {
            if (string.IsNullOrEmpty(value)) { return ""; }
            StringBuilder sb = new StringBuilder(12);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= '0' && c <= '9') { sb.Append(c); }
                else if (c >= 'A' && c <= 'F') { sb.Append(c); }
                else if (c >= 'a' && c <= 'f') { sb.Append(char.ToUpperInvariant(c)); }
            }
            return sb.ToString();
        }

        private static string FormatMac(string hex12)
        {
            if (hex12 == null || hex12.Length != 12) { return hex12 ?? ""; }
            return hex12.Substring(0, 2) + "-" + hex12.Substring(2, 2) + "-" +
                   hex12.Substring(4, 2) + "-" + hex12.Substring(6, 2) + "-" +
                   hex12.Substring(8, 2) + "-" + hex12.Substring(10, 2);
        }
    }
}
