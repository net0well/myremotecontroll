namespace MyRemoteControll.Contracts.Status;

/// <summary>
/// Uma amostra de métricas (spec 007). Campo nulo = contador indisponível (o app mostra "—"). Percentuais com uma
/// casa decimal e taxas em bytes por segundo inteiros, para manter o tráfego baixo.
/// </summary>
public sealed record MetricsSnapshot(
    DateTimeOffset At,
    CpuMetrics? Cpu,
    MemoryMetrics? Memory,
    IReadOnlyList<DiskMetrics> Disks,
    NetworkMetrics? Network,
    GpuMetrics? Gpu);

/// <param name="CoreUsagePercent">Por núcleo lógico; ausente no modo de dados reduzidos.</param>
public sealed record CpuMetrics(double? UsagePercent, IReadOnlyList<double>? CoreUsagePercent, double? ClockMhz);

public sealed record MemoryMetrics(long UsedBytes, long TotalBytes);

public sealed record DiskMetrics(
    string Drive,
    string? Label,
    long UsedBytes,
    long TotalBytes,
    long? ReadBytesPerSecond,
    long? WriteBytesPerSecond);

/// <param name="Interfaces">Por interface (sem as virtuais); ausente no modo de dados reduzidos.</param>
public sealed record NetworkMetrics(
    long DownBytesPerSecond,
    long UpBytesPerSecond,
    IReadOnlyList<NetworkInterfaceMetrics>? Interfaces);

public sealed record NetworkInterfaceMetrics(string Name, NetworkKind Kind, long DownBytesPerSecond, long UpBytesPerSecond);

/// <param name="DedicatedTotalBytes">Sem contador no Windows; preenchido só quando a fonte souber.</param>
public sealed record GpuMetrics(string? Name, double? UsagePercent, long? DedicatedUsedBytes, long? DedicatedTotalBytes);
