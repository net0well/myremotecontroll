using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyRemoteControll.Contracts.Devices;

namespace MyRemoteControll.Agent.Domain.Devices;

/// <summary>Aparelho pareado (docs/architecture/data-model.md). O token nunca é guardado, só o HMAC dele.</summary>
public sealed class Device
{
    /// <summary>Validade do token antes de o app ser orientado a rotacioná-lo.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(30);

    /// <summary>Tempo em que o token anterior ainda vale depois de uma rotação (cobre reconexões em andamento).</summary>
    public static readonly TimeSpan PreviousTokenGracePeriod = TimeSpan.FromHours(24);

    public Guid Id { get; init; }

    public required string Name { get; set; }

    public DevicePlatform Platform { get; init; }

    public DeviceRole Role { get; set; }

    public required byte[] TokenHash { get; set; }

    public DateTimeOffset TokenRotatedAt { get; set; }

    public byte[]? PreviousTokenHash { get; set; }

    public DateTimeOffset? PreviousTokenExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastSeenAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsRevoked => RevokedAt is not null;

    public void RotateToken(byte[] newTokenHash, DateTimeOffset now)
    {
        PreviousTokenHash = TokenHash;
        PreviousTokenExpiresAt = now + PreviousTokenGracePeriod;
        TokenHash = newTokenHash;
        TokenRotatedAt = now;
    }
}

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Devices");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(100);
        builder.Property(d => d.Platform).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.TokenHash).HasMaxLength(32);
        builder.Property(d => d.PreviousTokenHash).HasMaxLength(32);
        builder.HasIndex(d => d.TokenHash).IsUnique();
        builder.HasIndex(d => d.PreviousTokenHash);
        builder.Ignore(d => d.IsRevoked);
    }
}
