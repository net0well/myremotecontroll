namespace MyRemoteControll.Contracts.Status;

public enum NetworkKind
{
    Ethernet,
    Wifi,
    Tailscale,
    Vpn,
    Other,
}

/// <summary>Estado geral do PC (spec 007). <see cref="BootedAt"/> em vez de uptime: o app calcula o tempo ligado.</summary>
public sealed record SystemStatus(
    string Hostname,
    string OsName,
    string OsVersion,
    DateTimeOffset BootedAt,
    string UserName,
    IReadOnlyList<NetworkAddressDto> Addresses,
    BatteryStatusDto? Battery,
    PowerSupplyDto Power);

public sealed record NetworkAddressDto(string InterfaceName, NetworkKind Kind, string Address);

/// <param name="SecondsRemaining">Estimativa do Windows; ausente quando carregando ou desconhecida.</param>
public sealed record BatteryStatusDto(int Percent, bool Charging, int? SecondsRemaining);

public sealed record PowerSupplyDto(bool OnAcPower, string? PowerPlan);
