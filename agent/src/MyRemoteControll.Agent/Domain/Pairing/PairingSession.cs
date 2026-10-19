using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyRemoteControll.Agent.Domain.Pairing;

/// <summary>Oferta de pareamento exibida em QR: código de uso único, válido por <see cref="Lifetime"/>.</summary>
public sealed class PairingSession
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public Guid Id { get; init; }

    public required byte[] OneTimeCodeHash { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public Guid? ConsumedByDeviceId { get; set; }

    /// <summary>Pin SHA-256 da SPKI do certificado no momento da oferta (o mesmo que vai no QR).</summary>
    public required string SpkiPin { get; init; }
}

internal sealed class PairingSessionConfiguration : IEntityTypeConfiguration<PairingSession>
{
    public void Configure(EntityTypeBuilder<PairingSession> builder)
    {
        builder.ToTable("PairingSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.OneTimeCodeHash).HasMaxLength(32);
        builder.HasIndex(s => s.OneTimeCodeHash).IsUnique();
        builder.Property(s => s.SpkiPin).HasMaxLength(64);
    }
}
