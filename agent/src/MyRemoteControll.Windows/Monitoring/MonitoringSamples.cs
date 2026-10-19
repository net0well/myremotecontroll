namespace MyRemoteControll.Windows.Monitoring;

public enum InterfaceKind
{
    Ethernet,
    Wifi,
    Tailscale,
    Vpn,

    /// <summary>Adaptadores de máquinas virtuais e WSL (Hyper-V, VMware, VirtualBox): fora dos totais.</summary>
    Virtual,
    Loopback,
    Other,
}

/// <summary>Uma amostra crua do Windows; valores nulos = contador indisponível (ou primeira coleta de uma taxa).</summary>
public sealed record MetricsSample(
    CpuSample? Cpu,
    MemorySample? Memory,
    IReadOnlyList<DiskSample> Disks,
    IReadOnlyList<NetworkInterfaceSample> Interfaces,
    GpuSample? Gpu);

public sealed record CpuSample(double? TotalPercent, IReadOnlyList<double> CorePercent, double? ClockMhz);

public sealed record MemorySample(long UsedBytes, long TotalBytes);

public sealed record DiskSample(string Drive, string? Label, long UsedBytes, long TotalBytes, double? ReadBytesPerSecond, double? WriteBytesPerSecond);

public sealed record NetworkInterfaceSample(string Name, InterfaceKind Kind, double DownBytesPerSecond, double UpBytesPerSecond);

public sealed record GpuSample(string? Name, double? UsagePercent, long? DedicatedUsedBytes);

/// <summary>Estado geral do PC, lido sob demanda.</summary>
public sealed record SystemStatusSample(
    string Hostname,
    string OsName,
    string OsVersion,
    DateTimeOffset BootedAt,
    string UserName,
    IReadOnlyList<InterfaceAddress> Addresses,
    BatterySample? Battery,
    bool OnAcPower,
    string? PowerPlan);

public sealed record InterfaceAddress(string InterfaceName, InterfaceKind Kind, string Address);

public sealed record BatterySample(int Percent, bool Charging, int? SecondsRemaining);

/// <summary>Métricas do PC (spec 007). Cada chamada coleta uma amostra; quem decide o ritmo é o agente.</summary>
public interface IMetricsSource
{
    MetricsSample Sample();
}

public interface ISystemStatusSource
{
    SystemStatusSample GetStatus();
}
