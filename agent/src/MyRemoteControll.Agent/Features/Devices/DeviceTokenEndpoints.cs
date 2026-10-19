using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyRemoteControll.Agent.Domain.Audit;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Agent.Infrastructure.Auth;
using MyRemoteControll.Agent.Infrastructure.Logging;
using MyRemoteControll.Agent.Infrastructure.Persistence;
using MyRemoteControll.Agent.Infrastructure.Security;
using MyRemoteControll.Agent.Infrastructure.Web;
using MyRemoteControll.Contracts.Pairing;

namespace MyRemoteControll.Agent.Features.Devices;

internal sealed class DeviceTokenEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/v1/devices/me/rotate-token", RotateAsync)
            .RequireAuthorization() // Qualquer aparelho autenticado (o próprio token); explícito, sem depender da policy padrão.
            .WithName("RotateDeviceToken")
            .WithSummary("Emite um token novo; o anterior continua válido por 24 h (ADR-0008).");

    private static async Task<Ok<RotateTokenResponse>> RotateAsync(
        HttpContext http,
        AgentDbContext db,
        TokenHasher hasher,
        TimeProvider timeProvider,
        IAuditLog auditLog,
        CancellationToken cancellationToken)
    {
        // A política padrão exige aparelho autenticado, então o ID sempre existe aqui.
        var deviceId = http.User.GetDeviceId()!.Value;
        var device = await db.Devices.FirstAsync(d => d.Id == deviceId, cancellationToken).ConfigureAwait(false);

        var token = SecureTokens.Create(SecureTokens.DeviceTokenBytes);
        device.RotateToken(hasher.Hash(token), timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditLog.WriteAsync(
            new AuditRecord("devices.rotateToken", AuditResult.Success)
            {
                DeviceId = deviceId,
                CorrelationId = CorrelationIdMiddleware.Get(http),
            },
            cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new RotateTokenResponse(token, device.PreviousTokenExpiresAt!.Value));
    }
}
