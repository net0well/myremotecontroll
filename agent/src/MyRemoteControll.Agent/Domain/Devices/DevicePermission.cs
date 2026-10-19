using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyRemoteControll.Agent.Domain.Devices;

/// <summary>Permissão extra concedida a um aparelho, além das do papel (ADR-0009).</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Nome do modelo de dados aprovado (data-model.md); não é uma permissão de segurança do .NET.")]
public sealed class DevicePermission
{
    public Guid DeviceId { get; init; }

    public required string PermissionKey { get; init; }
}

internal sealed class DevicePermissionConfiguration : IEntityTypeConfiguration<DevicePermission>
{
    public void Configure(EntityTypeBuilder<DevicePermission> builder)
    {
        builder.ToTable("DevicePermissions");
        builder.HasKey(p => new { p.DeviceId, p.PermissionKey });
        builder.Property(p => p.PermissionKey).HasMaxLength(64);
        builder.HasOne<Device>().WithMany().HasForeignKey(p => p.DeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}
