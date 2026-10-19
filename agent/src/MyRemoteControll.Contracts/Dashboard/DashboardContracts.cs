using System.Text.Json;
using MyRemoteControll.Contracts.Power;
using MyRemoteControll.Contracts.Status;

namespace MyRemoteControll.Contracts.Dashboard;

/// <param name="MetricsIntervalMs">Intervalo dos cards de métricas, de 500 a 10000 ms.</param>
/// <param name="IncludeScreenThumbnail">Reservado para a spec 028.</param>
/// <param name="LowDataMode">Cards de métricas sem detalhes (núcleos, interfaces), para dados móveis.</param>
public sealed record DashboardOptions(int MetricsIntervalMs, bool IncludeScreenThumbnail, bool LowDataMode);

/// <summary>Estado completo do painel para esta conexão; os deltas seguintes começam em <c>Version + 1</c>.</summary>
public sealed record DashboardSnapshot(long Version, DateTimeOffset At, IReadOnlyList<CardState> Cards);

/// <param name="Data">Conteúdo do card, conforme o <see cref="Kind"/>.</param>
/// <param name="AvailableActions">Ações rápidas que este aparelho pode usar agora (permissão e capacidade do PC).</param>
public sealed record CardState(string CardId, string Kind, JsonElement Data, IReadOnlyList<string> AvailableActions);

/// <summary>Troca um card inteiro; <see cref="Card"/> nulo = card removido (ex.: perdeu a permissão de leitura).</summary>
public sealed record CardPatch(string CardId, CardState? Card);

/// <summary>Ids e tipos dos cards do painel v1.</summary>
public static class DashboardCards
{
    public const string Pc = "pc";
    public const string Power = "power";
    public const string Cpu = "cpu";
    public const string Memory = "memory";
    public const string Network = "network";
    public const string Disks = "disks";
    public const string Gpu = "gpu";
    public const string Battery = "battery";
}

/// <summary>Ações rápidas do card de energia (ids em <see cref="CardState.AvailableActions"/>).</summary>
public static class PowerCardActions
{
    public const string Lock = "power.lock";
    public const string Sleep = "power.sleep";
    public const string Restart = "power.restart";
    public const string Hibernate = "power.hibernate";
    public const string Shutdown = "power.shutdown";
    public const string Cancel = "power.cancel";
}

/// <summary>Conteúdo do card de energia: contagem ativa (se houver) e o que o PC oferece.</summary>
public sealed record PowerCardData(PowerCountdownDto? Pending, bool SleepAvailable, bool HibernateAvailable, bool UpdatePending);

/// <summary>Conteúdo do card "PC e conexão" (o status sem a bateria, que tem card próprio).</summary>
public sealed record PcCardData(
    string Hostname,
    string OsName,
    string OsVersion,
    DateTimeOffset BootedAt,
    string UserName,
    IReadOnlyList<NetworkAddressDto> Addresses,
    bool OnAcPower,
    string? PowerPlan);

/// <summary>Conteúdo do card de discos (um item por unidade fixa).</summary>
public sealed record DisksCardData(IReadOnlyList<DiskMetrics> Disks);
