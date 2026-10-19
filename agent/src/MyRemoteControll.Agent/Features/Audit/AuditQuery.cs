using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using MyRemoteControll.Agent.Domain.Audit;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Agent.Infrastructure.Persistence;
using MyRemoteControll.Contracts.Audit;

namespace MyRemoteControll.Agent.Features.Audit;

internal sealed record AuditFilter(DateTimeOffset? From, DateTimeOffset? To, Guid? DeviceId, string? Action, AuditResult? Result);

/// <summary>Consulta da auditoria para o app, a bandeja e a exportação (spec 003).</summary>
internal sealed class AuditQuery(IDbContextFactory<AgentDbContext> dbFactory)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    /// <summary>Do mais recente para o mais antigo; o cursor é o ID do último item da página anterior.</summary>
    public async Task<AuditPage> PageAsync(AuditFilter filter, string? cursor, int? limit, CancellationToken cancellationToken)
    {
        var size = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var query = Apply(db.AuditEntries.AsNoTracking(), filter);
        if (long.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var before))
        {
            query = query.Where(a => a.Id < before);
        }

        var entries = await query.OrderByDescending(a => a.Id).Take(size + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        var names = await DeviceNamesAsync(db, cancellationToken).ConfigureAwait(false);
        var hasMore = entries.Count > size;
        var page = entries.Take(size).Select(e => AuditEntryMapping.ToDto(e, names)).ToList();

        return new AuditPage(page, hasMore ? page[^1].Id.ToString(CultureInfo.InvariantCulture) : null);
    }

    /// <summary>Todos os registros do filtro, do mais antigo para o mais recente, sem carregar tudo na memória.</summary>
    public async IAsyncEnumerable<AuditEntryDto> StreamAsync(
        AuditFilter filter,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var names = await DeviceNamesAsync(db, cancellationToken).ConfigureAwait(false);
        await foreach (var entry in Apply(db.AuditEntries.AsNoTracking(), filter)
                           .OrderBy(a => a.Id)
                           .AsAsyncEnumerable()
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return AuditEntryMapping.ToDto(entry, names);
        }
    }

    private static IQueryable<AuditEntry> Apply(IQueryable<AuditEntry> query, AuditFilter filter)
    {
        // Datas gravadas em UTC: o filtro também é convertido para comparar no mesmo referencial.
        if (filter.From is { } from)
        {
            var fromUtc = from.ToUniversalTime();
            query = query.Where(a => a.Timestamp >= fromUtc);
        }

        if (filter.To is { } to)
        {
            var toUtc = to.ToUniversalTime();
            query = query.Where(a => a.Timestamp <= toUtc);
        }

        if (filter.DeviceId is { } deviceId)
        {
            query = query.Where(a => a.DeviceId == deviceId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(a => a.Action == filter.Action);
        }

        if (filter.Result is { } result)
        {
            query = query.Where(a => a.Result == result);
        }

        return query;
    }

    private static async Task<Dictionary<Guid, string>> DeviceNamesAsync(AgentDbContext db, CancellationToken cancellationToken) =>
        await db.Devices.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken).ConfigureAwait(false);
}
