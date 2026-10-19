using System.Text.Json;

namespace MyRemoteControll.Contracts.Audit;

public sealed record AuditEntryDto(
    long Id,
    DateTimeOffset Timestamp,
    string Source,
    Guid? DeviceId,
    string? DeviceName,
    string Action,
    JsonElement? Target,
    string Result,
    string? ErrorCode,
    bool LocalConfirmation,
    string? CorrelationId);

/// <summary>Página de auditoria, da mais recente para a mais antiga; <see cref="NextCursor"/> nulo encerra a lista.</summary>
public sealed record AuditPage(IReadOnlyList<AuditEntryDto> Items, string? NextCursor);
