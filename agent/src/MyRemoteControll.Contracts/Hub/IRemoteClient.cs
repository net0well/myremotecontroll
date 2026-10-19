using MyRemoteControll.Contracts.Audit;
using MyRemoteControll.Contracts.Dashboard;
using MyRemoteControll.Contracts.Devices;
using MyRemoteControll.Contracts.Power;
using MyRemoteControll.Contracts.Status;

namespace MyRemoteControll.Contracts.Hub;

/// <summary>Eventos que o agente envia ao app pelo Hub <c>/hubs/remote</c>.</summary>
public interface IRemoteClient
{
    Task AgentShuttingDown(string reason);

    /// <summary>Enviado ao próprio aparelho logo antes de ele ser desconectado por revogação.</summary>
    Task DeviceRevoked(Guid deviceId);

    Task DevicePermissionsChanged(DevicePermissionsChangedDto change);

    /// <summary>Novo registro de auditoria, para quem tem <c>security.viewAudit</c>.</summary>
    Task AuditAppended(AuditEntryDto entry);

    /// <summary>Ação de energia agendada (spec 005), para todos.</summary>
    Task PowerCountdown(PowerCountdownDto countdown);

    /// <summary>Contagem cancelada (por aparelho, pelo PC, por encerramento do agente ou falha do Windows), para todos.</summary>
    Task PowerCancelled(PowerCancelledDto cancelled);

    /// <summary>Cards que mudaram para esta conexão (spec 007); a versão segue a do snapshot, de 1 em 1.</summary>
    Task DashboardDelta(long version, IReadOnlyList<CardPatch> patches);

    /// <summary>O status do PC mudou, para <c>perm:status.view</c>.</summary>
    Task StatusUpdated(SystemStatus status);

    /// <summary>Amostra de métricas para quem chamou <c>SubscribeMetrics</c>.</summary>
    Task MetricsTick(MetricsSnapshot metrics);
}
