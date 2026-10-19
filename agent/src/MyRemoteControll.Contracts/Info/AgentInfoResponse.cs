using System.Text.Json.Serialization;
using MyRemoteControll.Contracts.Devices;

namespace MyRemoteControll.Contracts.Info;

/// <summary>
/// Resposta de <c>GET /api/v1/info</c>. Sem autenticação, só <see cref="ApiVersion"/> e <see cref="RequiresPairing"/>
/// são enviados; os demais campos aparecem apenas para um aparelho autenticado.
/// </summary>
public sealed record AgentInfoResponse(string ApiVersion, bool RequiresPairing)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AgentVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PcName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Capabilities { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? DeviceId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeviceRole? Role { get; init; }

    /// <summary>Permissões efetivas do aparelho (papel + extras); o app usa para esconder ações não permitidas.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Permissions { get; init; }

    /// <summary>O token tem mais de 30 dias: o app deve chamar <c>POST /api/v1/devices/me/rotate-token</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? TokenRotationDue { get; init; }
}
