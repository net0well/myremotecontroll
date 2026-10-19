using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyRemoteControll.Agent.Infrastructure.Persistence;

internal sealed class AgentSettingConfiguration : IEntityTypeConfiguration<AgentSetting>
{
    public void Configure(EntityTypeBuilder<AgentSetting> builder)
    {
        builder.ToTable("AgentSettings");
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasMaxLength(200);
        builder.Property(s => s.ValueJson).IsRequired();
    }
}
