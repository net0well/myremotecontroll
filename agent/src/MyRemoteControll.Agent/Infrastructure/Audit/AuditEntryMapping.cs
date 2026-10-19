using System.Text.Json;
using MyRemoteControll.Agent.Domain.Audit;
using MyRemoteControll.Contracts.Audit;

namespace MyRemoteControll.Agent.Infrastructure.Audit;

/// <summary>Registro de auditoria → contrato, usado pela consulta (REST, CSV) e pelo evento ao vivo <c>AuditAppended</c>.</summary>
internal static class AuditEntryMapping
{
    public static AuditEntryDto ToDto(AuditEntry entry, IReadOnlyDictionary<Guid, string>? deviceNames = null) =>
        new(
            entry.Id,
            entry.Timestamp,
            entry.Source.ToString(),
            entry.DeviceId,
            entry.DeviceId is { } id && deviceNames?.TryGetValue(id, out var name) == true ? name : null,
            entry.Action,
            entry.TargetJson is null ? null : JsonDocument.Parse(entry.TargetJson).RootElement.Clone(),
            entry.Result.ToString(),
            entry.ErrorCode,
            entry.LocalConfirmation,
            entry.CorrelationId);
}
