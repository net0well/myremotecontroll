using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using MyRemoteControll.Agent.Infrastructure.Persistence;
using MyRemoteControll.Contracts.Devices;
using MyRemoteControll.Contracts.Security;

namespace MyRemoteControll.Agent.Infrastructure.Authorization;

/// <summary>Papel e permissões efetivas de um aparelho ativo: as do papel somadas às extras (ADR-0009).</summary>
internal sealed record DeviceAccess(Guid DeviceId, DeviceRole Role, IReadOnlySet<string> ExtraPermissions)
{
    public IReadOnlySet<string> Permissions { get; } =
        RolePermissions.For(Role).Union(ExtraPermissions).ToHashSet(StringComparer.Ordinal);

    public bool Has(string permission) => Permissions.Contains(permission);
}

/// <summary>
/// Resolve as permissões a cada chamada, com cache invalidado quando o aparelho muda: uma alteração vale na próxima
/// chamada, sem esperar o aparelho reconectar (spec 003, AC-4). As claims da conexão não são usadas para isso.
/// </summary>
internal sealed class DevicePermissionResolver(IDbContextFactory<AgentDbContext> dbFactory)
{
    private readonly ConcurrentDictionary<Guid, DeviceAccess> _cache = new();
    private long _generation;

    /// <returns>Nulo se o aparelho não existe ou foi revogado.</returns>
    public async ValueTask<DeviceAccess?> GetAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(deviceId, out var cached))
        {
            return cached;
        }

        // Uma invalidação durante a consulta descarta o resultado, para não guardar no cache um estado já velho.
        var generation = Interlocked.Read(ref _generation);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var device = await db.Devices.AsNoTracking()
            .Where(d => d.Id == deviceId && d.RevokedAt == null)
            .Select(d => new { d.Role })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (device is null)
        {
            return null;
        }

        var extras = await db.DevicePermissions.AsNoTracking()
            .Where(p => p.DeviceId == deviceId)
            .Select(p => p.PermissionKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var access = new DeviceAccess(deviceId, device.Role, extras.ToHashSet(StringComparer.Ordinal));
        if (Interlocked.Read(ref _generation) == generation)
        {
            _cache[deviceId] = access;
        }

        return access;
    }

    public void Invalidate(Guid deviceId)
    {
        Interlocked.Increment(ref _generation);
        _cache.TryRemove(deviceId, out _);
    }
}
