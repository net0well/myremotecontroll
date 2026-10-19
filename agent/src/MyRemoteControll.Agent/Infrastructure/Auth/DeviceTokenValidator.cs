using Microsoft.EntityFrameworkCore;
using MyRemoteControll.Agent.Domain.Devices;
using MyRemoteControll.Agent.Infrastructure.Persistence;
using MyRemoteControll.Agent.Infrastructure.Security;

namespace MyRemoteControll.Agent.Infrastructure.Auth;

internal enum TokenValidationStatus
{
    Valid,
    Invalid,
    Revoked,
}

internal sealed record TokenValidation(TokenValidationStatus Status, Device? Device = null);

/// <summary>Confere o token pelo HMAC (atual ou o anterior, dentro da carência de rotação) e o estado do aparelho.</summary>
internal sealed class DeviceTokenValidator(AgentDbContext db, TokenHasher hasher, TimeProvider timeProvider)
{
    private static readonly TimeSpan LastSeenResolution = TimeSpan.FromMinutes(1);

    public async Task<TokenValidation> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        if (!SecureTokens.HasValidFormat(token))
        {
            return new TokenValidation(TokenValidationStatus.Invalid);
        }

        var hash = hasher.Hash(token);
        var now = timeProvider.GetUtcNow();
        var device = await db.Devices
            .FirstOrDefaultAsync(d => d.TokenHash == hash || d.PreviousTokenHash == hash, cancellationToken)
            .ConfigureAwait(false);
        if (device is null || !Matches(device, hash, now))
        {
            return new TokenValidation(TokenValidationStatus.Invalid);
        }

        if (device.IsRevoked)
        {
            return new TokenValidation(TokenValidationStatus.Revoked, device);
        }

        if (device.LastSeenAt is null || now - device.LastSeenAt >= LastSeenResolution)
        {
            device.LastSeenAt = now;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new TokenValidation(TokenValidationStatus.Valid, device);
    }

    private static bool Matches(Device device, byte[] hash, DateTimeOffset now) =>
        TokenHasher.AreEqual(device.TokenHash, hash)
        || (device.PreviousTokenHash is { } previous
            && device.PreviousTokenExpiresAt > now
            && TokenHasher.AreEqual(previous, hash));
}
