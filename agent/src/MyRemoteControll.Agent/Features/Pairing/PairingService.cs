using Microsoft.EntityFrameworkCore;
using MyRemoteControll.Agent.Domain.Audit;
using MyRemoteControll.Agent.Domain.Devices;
using MyRemoteControll.Agent.Domain.Pairing;
using MyRemoteControll.Agent.Hosting;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Agent.Infrastructure.Persistence;
using MyRemoteControll.Agent.Infrastructure.Security;
using MyRemoteControll.Agent.Infrastructure.Tls;
using MyRemoteControll.Contracts.Devices;
using MyRemoteControll.Contracts.Errors;
using MyRemoteControll.Contracts.Pairing;

namespace MyRemoteControll.Agent.Features.Pairing;

internal sealed record PairingOffer(string Code, DateTimeOffset ExpiresAt, string Payload, IReadOnlyList<string> Hosts, int Port);

internal sealed record PairingResult(CompletePairingResponse? Response, int StatusCode, string? ErrorCode, string? Title)
{
    public static PairingResult Success(CompletePairingResponse response) => new(response, StatusCodes.Status201Created, null, null);

    public static PairingResult Failure(int statusCode, string errorCode, string title) => new(null, statusCode, errorCode, title);
}

internal sealed class DevicePairedEventArgs(string deviceName, DeviceRole role) : EventArgs
{
    public string DeviceName { get; } = deviceName;

    public DeviceRole Role { get; } = role;
}

/// <summary>
/// Pareamento por QR (spec 002, ADR-0008): oferta com código de uso único, confirmação no PC e emissão do token do
/// aparelho, que só existe na resposta — o banco guarda o HMAC.
/// </summary>
internal sealed class PairingService(
    IDbContextFactory<AgentDbContext> dbFactory,
    TokenHasher hasher,
    TimeProvider timeProvider,
    AgentCertificate certificate,
    AgentEndpointsAccessor endpoints,
    IHostAddressProvider hostAddresses,
    PairingConfirmationBroker confirmation,
    PairingLockout lockout,
    IAuditLog auditLog,
    AgentNotifications notifications)
{
    public const string AuditAction = "pairing.complete";

    public event EventHandler<DevicePairedEventArgs>? DevicePaired;

    /// <summary>Cria uma oferta nova e invalida as anteriores ainda não usadas.</summary>
    public async Task<PairingOffer> CreateOfferAsync(CancellationToken cancellationToken)
    {
        var code = SecureTokens.Create(SecureTokens.PairingCodeBytes);
        var now = timeProvider.GetUtcNow();

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
        {
            await db.PairingSessions
                .Where(s => s.ConsumedAt == null && s.ExpiresAt > now)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ExpiresAt, now), cancellationToken)
                .ConfigureAwait(false);

            db.PairingSessions.Add(new PairingSession
            {
                Id = Guid.CreateVersion7(now),
                OneTimeCodeHash = hasher.Hash(code),
                CreatedAt = now,
                ExpiresAt = now + PairingSession.Lifetime,
                SpkiPin = certificate.Pin,
            });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var hosts = await hostAddresses.GetHostsAsync(cancellationToken).ConfigureAwait(false);
        var port = endpoints.Current.PublicPort;
        return new PairingOffer(
            code,
            now + PairingSession.Lifetime,
            PairingPayload.Build(hosts, port, code, certificate.Pin, Environment.MachineName),
            hosts,
            port);
    }

    public async Task<PairingResult> CompleteAsync(
        CompletePairingRequest request,
        string? remoteAddress,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var audit = new AuditRecord(AuditAction, AuditResult.Failed)
        {
            Target = new { request.DeviceName, request.Platform, remoteAddress },
            CorrelationId = correlationId,
        };

        if (lockout.IsLocked(out var lockedUntil))
        {
            await auditLog.WriteAsync(audit with { Result = AuditResult.Denied, ErrorCode = ErrorCodes.PairingLocked }, cancellationToken).ConfigureAwait(false);
            return PairingResult.Failure(
                StatusCodes.Status429TooManyRequests,
                ErrorCodes.PairingLocked,
                $"Pareamento bloqueado até {lockedUntil.ToLocalTime():HH:mm} por excesso de tentativas inválidas.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var codeHash = hasher.Hash(request.Code);
        var session = await db.PairingSessions
            .FirstOrDefaultAsync(s => s.OneTimeCodeHash == codeHash, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return await FailAsync(audit, StatusCodes.Status400BadRequest, ErrorCodes.PairingInvalidCode, "Código de pareamento inválido.", cancellationToken).ConfigureAwait(false);
        }

        if (session.ConsumedAt is not null)
        {
            return await FailAsync(audit, StatusCodes.Status410Gone, ErrorCodes.PairingConsumed, "Este código já foi usado. Gere um novo QR no PC.", cancellationToken).ConfigureAwait(false);
        }

        if (session.ExpiresAt <= timeProvider.GetUtcNow())
        {
            return await FailAsync(audit, StatusCodes.Status410Gone, ErrorCodes.PairingExpired, "O código expirou. Gere um novo QR no PC.", cancellationToken).ConfigureAwait(false);
        }

        // Consome o código antes de pedir confirmação: um segundo aparelho com o mesmo QR recebe "já usado".
        DateTimeOffset? consumedAt = timeProvider.GetUtcNow();
        var claimed = await db.PairingSessions
            .Where(s => s.Id == session.Id && s.ConsumedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ConsumedAt, consumedAt), cancellationToken)
            .ConfigureAwait(false);
        if (claimed == 0)
        {
            return await FailAsync(audit, StatusCodes.Status410Gone, ErrorCodes.PairingConsumed, "Este código já foi usado. Gere um novo QR no PC.", cancellationToken).ConfigureAwait(false);
        }

        var activeDevices = await db.Devices.CountAsync(d => d.RevokedAt == null, cancellationToken).ConfigureAwait(false);
        var decision = await confirmation
            .RequestAsync(new PairingRequestInfo(request.DeviceName, request.Platform, remoteAddress, activeDevices), timeProvider, cancellationToken)
            .ConfigureAwait(false);
        switch (decision)
        {
            case PairingDecision.Rejected:
                await auditLog.WriteAsync(audit with { Result = AuditResult.Denied, ErrorCode = ErrorCodes.PairingRejected }, cancellationToken).ConfigureAwait(false);
                return PairingResult.Failure(StatusCodes.Status403Forbidden, ErrorCodes.PairingRejected, "O pareamento foi recusado no PC.");
            case PairingDecision.TimedOut:
                await auditLog.WriteAsync(audit with { ErrorCode = ErrorCodes.PairingConfirmationTimeout }, cancellationToken).ConfigureAwait(false);
                return PairingResult.Failure(
                    StatusCodes.Status408RequestTimeout,
                    ErrorCodes.PairingConfirmationTimeout,
                    "Ninguém confirmou o pareamento no PC a tempo. Gere um novo QR e tente de novo.");
        }

        var device = await CreateDeviceAsync(db, request, session, cancellationToken).ConfigureAwait(false);
        await auditLog.WriteAsync(audit with { Result = AuditResult.Success, DeviceId = device.Device.Id }, cancellationToken).ConfigureAwait(false);
        DevicePaired?.Invoke(this, new DevicePairedEventArgs(device.Device.Name, device.Device.Role));

        return PairingResult.Success(new CompletePairingResponse(device.Device.Id, device.Token, device.Device.Role, Environment.MachineName));
    }

    private async Task<(Device Device, string Token)> CreateDeviceAsync(
        AgentDbContext db,
        CompletePairingRequest request,
        PairingSession session,
        CancellationToken cancellationToken)
    {
        // Sem um administrador ativo, quem pareia (com presença física no PC) vira administrador; senão, só leitura.
        var hasActiveAdministrator = await db.Devices
            .AnyAsync(d => d.RevokedAt == null && d.Role == DeviceRole.Administrator, cancellationToken)
            .ConfigureAwait(false);

        var token = SecureTokens.Create(SecureTokens.DeviceTokenBytes);
        var now = timeProvider.GetUtcNow();
        var device = new Device
        {
            Id = Guid.CreateVersion7(now),
            Name = request.DeviceName.Trim(),
            Platform = request.Platform,
            Role = hasActiveAdministrator ? DeviceRole.ReadOnly : DeviceRole.Administrator,
            TokenHash = hasher.Hash(token),
            TokenRotatedAt = now,
            CreatedAt = now,
            LastSeenAt = now,
        };
        db.Devices.Add(device);
        session.ConsumedByDeviceId = device.Id;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (device, token);
    }

    private async Task<PairingResult> FailAsync(
        AuditRecord audit,
        int statusCode,
        string errorCode,
        string title,
        CancellationToken cancellationToken)
    {
        await auditLog.WriteAsync(audit with { ErrorCode = errorCode }, cancellationToken).ConfigureAwait(false);
        if (lockout.RegisterFailure())
        {
            notifications.Raise(
                "Pareamento bloqueado",
                $"Houve {PairingLockout.MaxFailures} tentativas inválidas de pareamento. Novos pareamentos ficam bloqueados por 15 minutos.");
        }

        return PairingResult.Failure(statusCode, errorCode, title);
    }
}
