using System.Net;
using System.Net.Sockets;
namespace Nimbus.Application.Helpers;

public static class IpMasker
{
    /// <summary>IPv4 "84.198.12.9" becomes "84.198.x.x"; IPv6 keeps its first two 16-bit groups ("2001:db8:x:x"). Null when unknown/invalid.</summary>
    public static string? Mask(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out var address))
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return $"{b[0]}.{b[1]}.x.x";
        }

        // First two 16-bit groups taken from the address bytes, so compressed forms
        // ("2001::dead:beef" -> "2001:0:x:x") are masked correctly.
        var bytes = address.GetAddressBytes();
        return $"{(bytes[0] << 8) | bytes[1]:x}:{(bytes[2] << 8) | bytes[3]:x}:x:x";
    }
}
