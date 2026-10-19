using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MyRemoteControll.Agent.Domain.Audit;
using MyRemoteControll.Agent.Hubs;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Agent.Infrastructure.Authorization;
using MyRemoteControll.Agent.Infrastructure.Persistence;
using MyRemoteControll.Contracts.Hub;

namespace MyRemoteControll.Agent.Features.Devices;

/// <summary>
/// Revogação lógica e imediata: o token deixa de valer na próxima chamada e as conexões abertas no Hub são derrubadas
/// (spec 002, AC-4). Quem chama (bandeja ou REST da spec 003) aplica as regras de permissão e de último administrador.
/// </summary>
internal sealed partial class DeviceRevocation(
    IDbContextFactory<AgentDbContext> dbFactory,
    IHubContext<RemoteHub, IRemoteClient> hubContext,
    DeviceConnections connections,
    DevicePermissionResolver permissions,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<DeviceRevocation> logger)
{
    private static readonly TimeSpan NotifyTimeout = TimeSpan.FromSeconds(1);

    /// <returns>Falso se o aparelho não existe ou já estava revogado.</returns>
    public async Task<bool> RevokeAsync(Guid deviceId, AuditRecord audit, CancellationToken cancellationToken)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
        {
            DateTimeOffset? now = timeProvider.GetUtcNow();
            var updated = await db.Devices
                .Where(d => d.Id == deviceId && d.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.RevokedAt, now), cancellationToken)
                .ConfigureAwait(false);
            if (updated == 0)
            {
                return false;
            }
        }

        permissions.Invalidate(deviceId);
        await auditLog.WriteAsync(audit with { Target = new { deviceId } }, cancellationToken).ConfigureAwait(false);

        try
        {
            await hubContext.Clients.Group(DeviceConnections.GroupFor(deviceId))
                .DeviceRevoked(deviceId)
                .WaitAsync(NotifyTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            LogNotifyTimedOut(deviceId);
        }

        connections.Abort(deviceId);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tempo esgotado avisando o aparelho {DeviceId} sobre a revogação; a conexão será derrubada assim mesmo")]
    private partial void LogNotifyTimedOut(Guid deviceId);
}
