using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyRemoteControll.Agent.Domain.Audit;

/// <summary>Registro de auditoria, somente inserção (spec 003).</summary>
public sealed class AuditEntry
{
    public long Id { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public AuditSource Source { get; init; }

    /// <summary>Nulo para ações locais e para chamadas remotas ainda sem aparelho (ex.: tentativa de pareamento).</summary>
    public Guid? DeviceId { get; init; }

    public required string Action { get; init; }

    /// <summary>Parâmetros sanitizados: nunca tokens, conteúdo de arquivos ou mensagens.</summary>
    public string? TargetJson { get; init; }

    public AuditResult Result { get; init; }

    public string? ErrorCode { get; init; }

    public bool LocalConfirmation { get; init; }

    public string? CorrelationId { get; init; }
}

public enum AuditSource
{
    /// <summary>Chamada remota (com ou sem aparelho autenticado).</summary>
    Device,
    Tray,
    Scheduler,
    System,
}

public enum AuditResult
{
    Success,
    Failed,
    Denied,
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Result).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Action).HasMaxLength(100);
        builder.Property(a => a.ErrorCode).HasMaxLength(100);
        builder.Property(a => a.CorrelationId).HasMaxLength(64);
        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => new { a.DeviceId, a.Timestamp });
        builder.HasIndex(a => a.Action);
    }
}
