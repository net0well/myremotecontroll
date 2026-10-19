using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MyRemoteControll.Agent.Domain.Audit;
using MyRemoteControll.Agent.Domain.Devices;
using MyRemoteControll.Agent.Hubs;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Agent.Infrastructure.Authorization;
using MyRemoteControll.Agent.Infrastructure.Persistence;
using MyRemoteControll.Agent.Infrastructure.Results;
using MyRemoteControll.Contracts.Devices;
using MyRemoteControll.Contracts.Errors;
using MyRemoteControll.Contracts.Hub;
using MyRemoteControll.Contracts.Security;

namespace MyRemoteControll.Agent.Features.Devices;

/// <summary>
/// Gerenciamento de aparelhos (spec 003), usado pelo REST, pelo Hub e pela bandeja: listar, mudar papel e permissões,
/// revogar. Nunca deixa o PC sem um administrador ativo.
/// </summary>
internal sealed class DeviceManagement(
    IDbContextFactory<AgentDbContext> dbFactory,
    DevicePermissionResolver permissions,
    DeviceConnections connections,
    DeviceRevocation revocation,
    IHubContext<RemoteHub, IRemoteClient> hubContext,
    IAuditLog auditLog) : IDisposable
{
    public const string UpdateAction = "devices.updatePermissions";
    public const string RevokeAction = "devices.revoke";

    private static readonly OperationError NotFound =
        new(ErrorCodes.DeviceNotFound, "Aparelho não encontrado.", StatusCodes.Status404NotFound);

    private static readonly OperationError LastAdmin = new(
        ErrorCodes.SecurityLastAdmin,
        "Este é o único administrador ativo. Promova outro aparelho antes de rebaixar ou revogar este.",
        StatusCodes.Status409Conflict);

    // Serializa mudanças de papel e revogações: duas em paralelo poderiam, juntas, remover o último administrador.
    private readonly SemaphoreSlim _changes = new(1, 1);

    public async Task<IReadOnlyList<DeviceDto>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var devices = await db.Devices.AsNoTracking().OrderBy(d => d.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
        var extras = (await db.DevicePermissions.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToLookup(p => p.DeviceId, p => p.PermissionKey);
        var connected = connections.ConnectedDevices;

        return [.. devices.Select(d => ToDto(d, extras[d.Id], connected.Contains(d.Id)))];
    }

    public async Task<OperationResult<DeviceDto>> UpdatePermissionsAsync(
        Guid deviceId,
        UpdateDevicePermissionsRequest request,
        AuditActor actor,
        CancellationToken cancellationToken)
    {
        await _changes.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var device = await db.Devices
                .FirstOrDefaultAsync(d => d.Id == deviceId && d.RevokedAt == null, cancellationToken)
                .ConfigureAwait(false);
            if (device is null)
            {
                return NotFound;
            }

            var audit = actor.Record(UpdateAction, AuditResult.Failed) with
            {
                Target = new { deviceId, role = request.Role, extraPermissions = request.ExtraPermissions },
            };
            if (device.Role == DeviceRole.Administrator
                && request.Role != DeviceRole.Administrator
                && !await HasAnotherActiveAdministratorAsync(db, deviceId, cancellationToken).ConfigureAwait(false))
            {
                await auditLog.WriteAsync(audit with { ErrorCode = LastAdmin.Code }, cancellationToken).ConfigureAwait(false);
                return LastAdmin;
            }

            var extras = request.ExtraPermissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            {
                device.Role = request.Role;
                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    device.Name = request.Name.Trim();
                }

                await db.DevicePermissions.Where(p => p.DeviceId == deviceId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
                db.DevicePermissions.AddRange(extras.Select(key => new DevicePermission { DeviceId = deviceId, PermissionKey = key }));
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            permissions.Invalidate(deviceId);
            await NotifyPermissionsChangedAsync(deviceId, cancellationToken).ConfigureAwait(false);
            await auditLog.WriteAsync(audit with { Result = AuditResult.Success }, cancellationToken).ConfigureAwait(false);

            return OperationResult<DeviceDto>.Ok(ToDto(device, extras, connections.ConnectedDevices.Contains(deviceId)));
        }
        finally
        {
            _changes.Release();
        }
    }

    public async Task<OperationResult<bool>> RevokeAsync(Guid deviceId, AuditActor actor, CancellationToken cancellationToken)
    {
        await _changes.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var device = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken).ConfigureAwait(false);
            if (device is null)
            {
                return NotFound;
            }

            if (device.IsRevoked)
            {
                return OperationResult<bool>.Ok(false);
            }

            if (device.Role == DeviceRole.Administrator
                && !await HasAnotherActiveAdministratorAsync(db, deviceId, cancellationToken).ConfigureAwait(false))
            {
                await auditLog.WriteAsync(
                    actor.Record(RevokeAction, AuditResult.Failed) with { Target = new { deviceId }, ErrorCode = LastAdmin.Code },
                    cancellationToken).ConfigureAwait(false);
                return LastAdmin;
            }

            var revoked = await revocation
                .RevokeAsync(deviceId, actor.Record(RevokeAction, AuditResult.Success), cancellationToken)
                .ConfigureAwait(false);
            return OperationResult<bool>.Ok(revoked);
        }
        finally
        {
            _changes.Release();
        }
    }

    public void Dispose() => _changes.Dispose();

    private async Task NotifyPermissionsChangedAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        if (await permissions.GetAsync(deviceId, cancellationToken).ConfigureAwait(false) is not { } access)
        {
            return;
        }

        // Atualiza os grupos de permissão das conexões abertas, para que os eventos sigam o novo papel.
        foreach (var connectionId in connections.ConnectionsOf(deviceId))
        {
            foreach (var permission in PermissionKeys.All)
            {
                var group = PermissionGroups.For(permission);
                await (access.Has(permission)
                        ? hubContext.Groups.AddToGroupAsync(connectionId, group, cancellationToken)
                        : hubContext.Groups.RemoveFromGroupAsync(connectionId, group, cancellationToken))
                    .ConfigureAwait(false);
            }
        }

        await hubContext.Clients.Group(DeviceConnections.GroupFor(deviceId))
            .DevicePermissionsChanged(new DevicePermissionsChangedDto(deviceId, access.Role, [.. access.Permissions.Order(StringComparer.Ordinal)]))
            .ConfigureAwait(false);
    }

    private static Task<bool> HasAnotherActiveAdministratorAsync(AgentDbContext db, Guid deviceId, CancellationToken cancellationToken) =>
        db.Devices.AnyAsync(
            d => d.Id != deviceId && d.RevokedAt == null && d.Role == DeviceRole.Administrator,
            cancellationToken);

    private static DeviceDto ToDto(Device device, IEnumerable<string> extras, bool connected)
    {
        var extraList = extras.Order(StringComparer.Ordinal).ToList();
        var effective = RolePermissions.For(device.Role).Union(extraList).Order(StringComparer.Ordinal).ToList();
        return new DeviceDto(
            device.Id,
            device.Name,
            device.Platform,
            device.Role,
            extraList,
            effective,
            device.CreatedAt,
            device.LastSeenAt,
            connected,
            device.IsRevoked);
    }
}
