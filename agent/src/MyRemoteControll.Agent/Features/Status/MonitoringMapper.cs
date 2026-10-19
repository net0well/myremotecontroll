using MyRemoteControll.Contracts.Dashboard;
using MyRemoteControll.Contracts.Status;
using MyRemoteControll.Windows.Monitoring;

namespace MyRemoteControll.Agent.Features.Status;

/// <summary>
/// Amostras do Windows → contratos. Arredonda na origem (percentuais com 1 casa, bytes inteiros), para o JSON ficar
/// curto (spec 007, AC-7).
/// </summary>
internal static class MonitoringMapper
{
    public static MetricsSnapshot ToMetrics(MetricsSample sample, DateTimeOffset at) =>
        new(
            at,
            sample.Cpu is { } cpu ? new CpuMetrics(Round(cpu.TotalPercent), [.. cpu.CorePercent.Select(Round1)], RoundWhole(cpu.ClockMhz)) : null,
            sample.Memory is { } memory ? new MemoryMetrics(memory.UsedBytes, memory.TotalBytes) : null,
            [.. sample.Disks.Select(d => new DiskMetrics(d.Drive, d.Label, d.UsedBytes, d.TotalBytes, Bytes(d.ReadBytesPerSecond), Bytes(d.WriteBytesPerSecond)))],
            Network(sample.Interfaces),
            sample.Gpu is { } gpu ? new GpuMetrics(gpu.Name, Round(gpu.UsagePercent), gpu.DedicatedUsedBytes, null) : null);

    /// <summary>Versões para dados reduzidos: sem núcleos e sem interfaces.</summary>
    public static CpuMetrics? Lite(CpuMetrics? cpu) => cpu is null ? null : cpu with { CoreUsagePercent = null };

    public static NetworkMetrics? Lite(NetworkMetrics? network) => network is null ? null : network with { Interfaces = null };

    public static SystemStatus ToStatus(SystemStatusSample sample) =>
        new(
            sample.Hostname,
            sample.OsName,
            sample.OsVersion,
            sample.BootedAt,
            sample.UserName,
            [.. sample.Addresses.Select(a => new NetworkAddressDto(a.InterfaceName, Kind(a.Kind), a.Address))],
            sample.Battery is { } battery ? new BatteryStatusDto(battery.Percent, battery.Charging, battery.SecondsRemaining) : null,
            new PowerSupplyDto(sample.OnAcPower, sample.PowerPlan));

    public static PcCardData ToPcCard(SystemStatus status) =>
        new(status.Hostname, status.OsName, status.OsVersion, status.BootedAt, status.UserName, status.Addresses, status.Power.OnAcPower, status.Power.PowerPlan);

    /// <summary>
    /// Totais só das placas reais: o tráfego do Tailscale e das VPNs já passa pela placa física e contaria duas vezes.
    /// Sem nenhuma interface (primeira amostra), o card fica sem rede.
    /// </summary>
    private static NetworkMetrics? Network(IReadOnlyList<NetworkInterfaceSample> interfaces)
    {
        if (interfaces.Count == 0)
        {
            return null;
        }

        var physical = interfaces.Where(i => i.Kind is not (InterfaceKind.Tailscale or InterfaceKind.Vpn)).ToList();
        return new NetworkMetrics(
            Bytes(physical.Sum(i => i.DownBytesPerSecond))!.Value,
            Bytes(physical.Sum(i => i.UpBytesPerSecond))!.Value,
            [.. interfaces.Select(i => new NetworkInterfaceMetrics(i.Name, Kind(i.Kind), Bytes(i.DownBytesPerSecond)!.Value, Bytes(i.UpBytesPerSecond)!.Value))]);
    }

    private static NetworkKind Kind(InterfaceKind kind) => kind switch
    {
        InterfaceKind.Ethernet => NetworkKind.Ethernet,
        InterfaceKind.Wifi => NetworkKind.Wifi,
        InterfaceKind.Tailscale => NetworkKind.Tailscale,
        InterfaceKind.Vpn => NetworkKind.Vpn,
        _ => NetworkKind.Other,
    };

    private static double? Round(double? value) => value is { } v ? Round1(v) : null;

    private static double Round1(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    private static double? RoundWhole(double? value) => value is { } v ? Math.Round(v, MidpointRounding.AwayFromZero) : null;

    private static long? Bytes(double? value) => value is { } v ? (long)Math.Round(v, MidpointRounding.AwayFromZero) : null;
}
