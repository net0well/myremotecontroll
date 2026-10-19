using MyRemoteControll.Agent.Domain.Audit;

namespace MyRemoteControll.Agent.Infrastructure.Audit;

/// <summary>
/// Registra ações na auditoria. A gravação no banco acontece em lote, em segundo plano; a chamada só espera se a fila
/// estiver cheia (contrapressão), nunca descarta registros (spec 003).
/// </summary>
public interface IAuditLog
{
    ValueTask WriteAsync(AuditRecord record, CancellationToken cancellationToken = default);
}

/// <param name="Action">Ação no formato <c>area.verbo</c>, ex.: <c>pairing.complete</c>.</param>
public sealed record AuditRecord(string Action, AuditResult Result)
{
    public AuditSource Source { get; init; } = AuditSource.Device;

    public Guid? DeviceId { get; init; }

    /// <summary>Serializado como JSON. Nunca incluir tokens, conteúdo de arquivos ou mensagens.</summary>
    public object? Target { get; init; }

    public string? ErrorCode { get; init; }

    public bool LocalConfirmation { get; init; }

    public string? CorrelationId { get; init; }
}
