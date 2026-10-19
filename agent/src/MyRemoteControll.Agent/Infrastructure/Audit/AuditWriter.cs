using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MyRemoteControll.Agent.Domain.Audit;
using MyRemoteControll.Agent.Hubs;
using MyRemoteControll.Agent.Infrastructure.Logging;
using MyRemoteControll.Agent.Infrastructure.Persistence;
using MyRemoteControll.Contracts.Hub;
using MyRemoteControll.Contracts.Security;

namespace MyRemoteControll.Agent.Infrastructure.Audit;

/// <summary>
/// Fila de auditoria limitada, gravada em lote. Fila cheia faz quem escreve esperar (nunca descarta). No encerramento,
/// a fila é fechada e esvaziada antes de o processo sair (spec 003, AC-7).
/// </summary>
internal sealed partial class AuditWriter(
    IDbContextFactory<AgentDbContext> dbFactory,
    IHubContext<RemoteHub, IRemoteClient> hubContext,
    TimeProvider timeProvider,
    ILogger<AuditWriter> logger) : BackgroundService, IAuditLog
{
    public const int Capacity = 10_000;
    private const int MaxBatchSize = 200;

    private readonly Channel<AuditEntry> _queue = Channel.CreateBounded<AuditEntry>(
        new BoundedChannelOptions(Capacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public async ValueTask WriteAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        var entry = new AuditEntry
        {
            Timestamp = timeProvider.GetUtcNow(),
            Source = record.Source,
            DeviceId = record.DeviceId,
            Action = record.Action,
            TargetJson = record.Target is null ? null : JsonSerializer.Serialize(record.Target),
            Result = record.Result,
            ErrorCode = record.ErrorCode,
            LocalConfirmation = record.LocalConfirmation,
            CorrelationId = record.CorrelationId ?? CorrelationContext.Current,
        };

        try
        {
            await _queue.Writer.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            LogWriteAfterShutdown(record.Action);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Writer.TryComplete();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Não usa o stoppingToken para ler: a leitura termina quando a fila é fechada e esvaziada em StopAsync.
        List<AuditEntry> batch = new(MaxBatchSize);
        while (await _queue.Reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false))
        {
            while (batch.Count < MaxBatchSize && _queue.Reader.TryRead(out var entry))
            {
                batch.Add(entry);
            }

            await PersistAsync(batch).ConfigureAwait(false);
            batch.Clear();
        }
    }

    private async Task PersistAsync(List<AuditEntry> batch)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync().ConfigureAwait(false);
            db.AuditEntries.AddRange(batch);
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        {
            LogPersistFailed(ex, batch.Count);
            return;
        }

        await PublishAsync(batch).ConfigureAwait(false);
    }

    // Quem tem security.viewAudit acompanha a auditoria ao vivo (evento AuditAppended).
    private async Task PublishAsync(List<AuditEntry> batch)
    {
        var viewers = hubContext.Clients.Group(PermissionGroups.For(PermissionKeys.SecurityViewAudit));
        foreach (var entry in batch)
        {
            try
            {
                await viewers.AuditAppended(AuditEntryMapping.ToDto(entry)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException)
            {
                LogPublishFailed(ex);
                return;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao gravar {Count} registros de auditoria")]
    private partial void LogPersistFailed(Exception exception, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao publicar registros de auditoria no Hub")]
    private partial void LogPublishFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Registro de auditoria {Action} descartado: o agente já estava encerrando")]
    private partial void LogWriteAfterShutdown(string action);
}
