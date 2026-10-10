using System.Net;
using System.Net.Sockets;
namespace Nimbus.Application.Helpers;

public static class IpMasker
{
    /// <summary>IPv4 "84.198.12.9" becomes "84.198.x.x"; IPv6 keeps its first two groups ("2001:db8:x:x"). Null when unknown/invalid.</summary>
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

        var groups = address.ToString().Split(':', StringSplitOptions.RemoveEmptyEntries);
        return groups.Length >= 2 ? $"{groups[0]}:{groups[1]}:x:x" : "x:x";
    }
}
