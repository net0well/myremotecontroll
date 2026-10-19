using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyRemoteControll.Agent.Infrastructure.Dashboard;

/// <summary>Ação rápida de um card: vai para <c>availableActions</c> se o aparelho tem a permissão e o PC oferece a ação.</summary>
internal sealed record CardAction(string Id, string Permission, bool Available = true);

/// <summary>
/// Card publicado por uma feature no <see cref="DashboardAggregator"/> (ADR-0021).
/// </summary>
/// <param name="ReadPermission">Sem ela o card nem é enviado.</param>
/// <param name="Metrics">Segue o intervalo de métricas de cada inscrito; os demais vão assim que mudam.</param>
/// <param name="LiteData">Versão para dados reduzidos (sem detalhes); nula = igual a <paramref name="Data"/>.</param>
internal sealed record DashboardCard(
    string CardId,
    string Kind,
    string ReadPermission,
    JsonElement Data,
    IReadOnlyList<CardAction> Actions,
    bool Metrics = false,
    JsonElement? LiteData = null)
{
    public static DashboardCard Create<T>(
        string cardId,
        string readPermission,
        T data,
        IReadOnlyList<CardAction>? actions = null,
        bool metrics = false,
        T? lite = default) =>
        new(
            cardId,
            cardId,
            readPermission,
            JsonSerializer.SerializeToElement(data, DashboardJson.Options),
            actions ?? [],
            metrics,
            lite is null ? null : JsonSerializer.SerializeToElement(lite, DashboardJson.Options));
}

/// <summary>JSON dos cards: como o resto do Hub (camelCase, enums em texto), mas sem campos nulos, para economizar tráfego.</summary>
internal static class DashboardJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
