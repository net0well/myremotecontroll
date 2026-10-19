using System.Reflection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyRemoteControll.Agent.Domain.Devices;
using MyRemoteControll.Agent.Infrastructure.Auth;
using MyRemoteControll.Agent.Infrastructure.Authorization;
using MyRemoteControll.Agent.Infrastructure.Persistence;
using MyRemoteControll.Agent.Infrastructure.Web;
using MyRemoteControll.Contracts;
using MyRemoteControll.Contracts.Info;

namespace MyRemoteControll.Agent.Features.Info;

internal sealed class InfoEndpoints : IEndpointGroup
{
    /// <summary>Funcionalidades disponíveis nesta versão do agente; o app esconde o que não estiver aqui.</summary>
    private static readonly string[] Capabilities = ["pairing"];

    private static readonly string AgentVersion =
        typeof(InfoEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    public void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/info", GetInfoAsync)
            .AllowAnonymous()
            .WithName("GetInfo")
            .WithSummary("Versão do contrato; sem autenticação, só o necessário para parear.");

    private static async Task<Results<Ok<AgentInfoResponse>, ProblemHttpResult>> GetInfoAsync(
        HttpContext http,
        AgentDbContext db,
        DevicePermissionResolver permissions,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var anonymous = new AgentInfoResponse(ApiInfo.Version, RequiresPairing: true);
        if (http.User.GetDeviceId() is not { } deviceId)
        {
            // Sem token: informações de pareamento. Com token que não vale: 401, para o app saber que precisa reparear.
            return DeviceTokenAuthenticationHandler.HasCredentials(http)
                ? DeviceTokenAuthenticationHandler.Unauthorized(http)
                : TypedResults.Ok(anonymous);
        }

        var device = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken).ConfigureAwait(false);
        var access = await permissions.GetAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (device is null || access is null)
        {
            return DeviceTokenAuthenticationHandler.Unauthorized(http);
        }

        return TypedResults.Ok(anonymous with
        {
            RequiresPairing = false,
            AgentVersion = AgentVersion,
            PcName = Environment.MachineName,
            Capabilities = Capabilities,
            DeviceId = device.Id,
            Role = device.Role,
            Permissions = [.. access.Permissions.Order(StringComparer.Ordinal)],
            TokenRotationDue = timeProvider.GetUtcNow() - device.TokenRotatedAt >= Device.TokenLifetime,
        });
    }
}
