using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyRemoteControll.Agent.Infrastructure.Persistence;

namespace MyRemoteControll.Agent.Features.Audit;

/// <summary>Configuração da seção <c>Audit</c>.</summary>
public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    /// <summary>Por quantos dias a auditoria é mantida (spec 003: padrão 90).</summary>
    public int RetentionDays { get; set; } = 90;
}

/// <summary>Remove registros de auditoria mais antigos que a retenção, ao iniciar e depois uma vez por dia.</summary>
internal sealed partial class AuditRetention(
    IDbContextFactory<AgentDbContext> dbFactory,
    IOptionsMonitor<AuditOptions> options,
    TimeProvider timeProvider,
    ILogger<AuditRetention> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    public async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow().AddDays(-Math.Max(1, options.CurrentValue.RetentionDays));
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var removed = await db.AuditEntries.Where(a => a.Timestamp < cutoff).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        if (removed > 0)
        {
            LogPurged(removed, cutoff);
        }

        return removed;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await PurgeAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(Interval, timeProvider, stoppingToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Removed} registros de auditoria anteriores a {Cutoff} removidos pela retenção")]
    private partial void LogPurged(int removed, DateTimeOffset cutoff);
}
