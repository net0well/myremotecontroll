using System.Text.Json.Serialization;

namespace MyRemoteControll.Contracts.Hub;

/// <summary>
/// Retorno de todo método do Hub (Result pattern, hub-contract.md seção 1): <c>{ ok, data }</c> ou
/// <c>{ ok: false, error: { code, message, details? } }</c>. Crie com <see cref="HubResponse.Success{T}"/> e
/// <see cref="HubResponse.Failure{T}"/>.
/// </summary>
public sealed record HubResponse<T>(bool Ok, T? Data, HubError? Error);

/// <param name="Details">Dados do erro quando o código os prevê (ex.: a lista de <c>power.activeWork</c>).</param>
public sealed record HubError(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Details = null);

public static class HubResponse
{
    public static HubResponse<T> Success<T>(T data) => new(true, data, null);

    public static HubResponse<T> Failure<T>(string code, string message, object? details = null) =>
        new(false, default, new HubError(code, message, details));
}
