using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace CopperHead;

public sealed record NetworkAdapterChoice(
    string Name,
    int InterfaceIndex,
    IPAddress LocalAddress,
    IPAddress Gateway,
    string Description)
{
    public override string ToString() =>
        $"{Name}  |  {LocalAddress}  via  {Gateway}  (IF {InterfaceIndex})";
}

public static class NetworkAdapterEnumerator
{
    public static IReadOnlyList<NetworkAdapterChoice> GetChoices()
    {
        var results = new List<NetworkAdapterChoice>();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            var props = ni.GetIPProperties();
            var gateways = props.GatewayAddresses
                .Select(g => g.Address)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                .ToList();

            if (gateways.Count == 0)
                continue;

            var ipv4 = props.UnicastAddresses
                .Select(u => u.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);

            if (ipv4 is null)
                continue;

            var index = props.GetIPv4Properties()?.Index ?? 0;
            if (index <= 0)
                continue;

            foreach (var gw in gateways)
            {
                results.Add(new NetworkAdapterChoice(
                    ni.Name,
                    index,
                    ipv4,
                    gw,
                    ni.Description));
            }
        }

        return results
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
