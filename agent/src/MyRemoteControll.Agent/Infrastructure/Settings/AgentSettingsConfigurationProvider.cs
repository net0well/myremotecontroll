using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyRemoteControll.Agent.Infrastructure.Persistence;

namespace MyRemoteControll.Agent.Infrastructure.Settings;

/// <summary>
/// Expõe a tabela <c>AgentSettings</c> como fonte de configuração, por cima do appsettings.json.
/// Valores JSON de objeto viram chaves aninhadas (<c>Secao:Chave</c>), como no próprio appsettings.json.
/// </summary>
internal sealed class AgentSettingsConfigurationProvider(string connectionString) : ConfigurationProvider
{
    public override void Load()
    {
        using var db = new AgentDbContext(SqliteDatabase.ContextOptions(connectionString));
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var setting in db.AgentSettings.AsNoTracking())
        {
            using var document = JsonDocument.Parse(setting.ValueJson);
            Flatten(setting.Key, document.RootElement, data);
        }

        Data = data;
    }

    /// <summary>Relê o banco e avisa quem observa a configuração (ex.: <c>IOptionsMonitor</c>).</summary>
    public void Reload()
    {
        Load();
        OnReload();
    }

    private static void Flatten(string key, JsonElement element, Dictionary<string, string?> data)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Flatten($"{key}:{property.Name}", property.Value, data);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Flatten($"{key}:{index.ToString(CultureInfo.InvariantCulture)}", item, data);
                    index++;
                }

                break;
            case JsonValueKind.String:
                data[key] = element.GetString();
                break;
            case JsonValueKind.Null:
                data[key] = null;
                break;
            default:
                data[key] = element.GetRawText();
                break;
        }
    }
}
