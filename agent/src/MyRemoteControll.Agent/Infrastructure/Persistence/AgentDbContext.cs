using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyRemoteControll.Agent.Domain.Audit;
using MyRemoteControll.Agent.Domain.Devices;
using MyRemoteControll.Agent.Domain.Pairing;

namespace MyRemoteControll.Agent.Infrastructure.Persistence;

public sealed class AgentDbContext(DbContextOptions<AgentDbContext> options) : DbContext(options)
{
    public DbSet<AgentSetting> AgentSettings => Set<AgentSetting>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<DevicePermission> DevicePermissions => Set<DevicePermission>();

    public DbSet<PairingSession> PairingSessions => Set<PairingSession>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // O SQLite não ordena nem compara DateTimeOffset gravado como texto; como número, a ordenação funciona.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentDbContext).Assembly);
}
