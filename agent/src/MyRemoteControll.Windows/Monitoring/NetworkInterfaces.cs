using System.Net.NetworkInformation;

namespace MyRemoteControll.Windows.Monitoring;

/// <summary>Classifica interfaces de rede para o painel: Tailscale identificado, virtuais agrupadas e fora dos totais.</summary>
public static class NetworkInterfaces
{
    private static readonly string[] VirtualMarkers = ["Hyper-V", "vEthernet", "VMware", "VirtualBox", "WSL", "Docker"];
    private static readonly string[] VpnMarkers = ["VPN", "WireGuard", "TAP-Windows", "OpenVPN"];

    public static InterfaceKind Classify(string name, string description, NetworkInterfaceType type)
    {
        if (Contains(name, "Tailscale") || Contains(description, "Tailscale"))
        {
            return InterfaceKind.Tailscale;
        }

        if (type == NetworkInterfaceType.Loopback)
        {
            return InterfaceKind.Loopback;
        }

        if (VirtualMarkers.Any(marker => Contains(name, marker) || Contains(description, marker)))
        {
            return InterfaceKind.Virtual;
        }

        if (type is NetworkInterfaceType.Ppp or NetworkInterfaceType.Tunnel || VpnMarkers.Any(marker => Contains(description, marker)))
        {
            return InterfaceKind.Vpn;
        }

        return type switch
        {
            NetworkInterfaceType.Wireless80211 => InterfaceKind.Wifi,
            NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT
                or NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.Ethernet3Megabit => InterfaceKind.Ethernet,
            _ => InterfaceKind.Other,
        };
    }

    /// <summary>
    /// Interfaces ligadas que interessam ao usuário: sem loopback, sem as virtuais e só as que têm endereço IP. Os
    /// filtros do NDIS (WFP, QoS) e os WAN Miniport aparecem como interfaces "Ethernet" ligadas, mas sem endereço, e
    /// repetiriam o tráfego da placa real.
    /// </summary>
    public static IEnumerable<(NetworkInterface Interface, InterfaceKind Kind)> Relevant() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up && ni.GetIPProperties().UnicastAddresses.Count > 0)
            .Select(ni => (ni, Classify(ni.Name, ni.Description, ni.NetworkInterfaceType)))
            .Where(pair => pair.Item2 is not (InterfaceKind.Loopback or InterfaceKind.Virtual));

    private static bool Contains(string text, string marker) => text.Contains(marker, StringComparison.OrdinalIgnoreCase);
}
