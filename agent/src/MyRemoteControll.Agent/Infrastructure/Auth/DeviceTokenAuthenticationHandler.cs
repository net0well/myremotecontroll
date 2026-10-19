using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MyRemoteControll.Agent.Hubs;
using MyRemoteControll.Agent.Infrastructure.Web;
using MyRemoteControll.Contracts.Errors;

namespace MyRemoteControll.Agent.Infrastructure.Auth;

internal static class DeviceTokenDefaults
{
    public const string AuthenticationScheme = "DeviceToken";
}

/// <summary>
/// Autenticação por token de dispositivo (ADR-0008): <c>Authorization: Bearer</c> no REST e na negociação do Hub;
/// <c>access_token</c> na query só nas rotas do Hub, porque o WebSocket não envia headers personalizados.
/// </summary>
internal sealed class DeviceTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    DeviceTokenValidator validator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string BearerPrefix = "Bearer ";
    private static readonly object FailureCodeKey = new();

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadToken();
        if (token is null)
        {
            return AuthenticateResult.NoResult();
        }

        var validation = await validator.ValidateAsync(token, Context.RequestAborted).ConfigureAwait(false);
        switch (validation.Status)
        {
            case TokenValidationStatus.Valid:
                return AuthenticateResult.Success(
                    new AuthenticationTicket(DeviceClaims.CreatePrincipal(validation.Device!, Scheme.Name), Scheme.Name));
            case TokenValidationStatus.Revoked:
                Context.Items[FailureCodeKey] = ErrorCodes.AuthRevoked;
                return AuthenticateResult.Fail("Aparelho revogado.");
            default:
                return AuthenticateResult.Fail("Token inválido.");
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        Unauthorized(Context).ExecuteAsync(Context);

    /// <summary>
    /// 401 em ProblemDetails com <c>auth.revoked</c> ou <c>auth.unauthorized</c>. Também usado por endpoints anônimos
    /// quando o cliente envia um token que não vale, para que ele saiba que precisa reparear.
    /// </summary>
    public static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Unauthorized(HttpContext context)
    {
        var code = context.Items[FailureCodeKey] as string ?? ErrorCodes.AuthUnauthorized;
        var title = code == ErrorCodes.AuthRevoked
            ? "Este aparelho foi revogado no PC. Pareie novamente."
            : "Autenticação necessária.";
        return ApiProblems.Create(StatusCodes.Status401Unauthorized, code, title);
    }

    /// <summary>O cliente mandou credencial (header ou query do Hub), válida ou não.</summary>
    public static bool HasCredentials(HttpContext context) =>
        context.Request.Headers.Authorization.Count > 0
        || (context.Request.Path.StartsWithSegments(RemoteHub.Path) && context.Request.Query.ContainsKey("access_token"));

    private string? ReadToken()
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return header[BearerPrefix.Length..].Trim();
        }

        if (Request.Path.StartsWithSegments(RemoteHub.Path) && Request.Query.TryGetValue("access_token", out var queryToken))
        {
            return queryToken.ToString();
        }

        return null;
    }
}
