using System.Text.Json;
using MyRemoteControll.Agent.Infrastructure.Persistence;

namespace MyRemoteControll.Agent.Infrastructure.Settings;

/// <summary>Grava configurações em <c>AgentSettings</c> e recarrega a fonte de configuração correspondente.</summary>
internal sealed class AgentSettingsStore(
    string connectionString,
    AgentSettingsConfigurationProvider provider,
    TimeProvider timeProvider)
{
    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var json = JsonSerializer.Serialize(value);
        await using (var db = new AgentDbContext(SqliteDatabase.ContextOptions(connectionString)))
        {
            var setting = await db.AgentSettings.FindAsync([key], cancellationToken).ConfigureAwait(false);
            if (setting is null)
            {
                db.AgentSettings.Add(new AgentSetting { Key = key, ValueJson = json, UpdatedAt = timeProvider.GetUtcNow() });
            }
            else
            {
                setting.ValueJson = json;
                setting.UpdatedAt = timeProvider.GetUtcNow();
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        provider.Reload();
    }
}
