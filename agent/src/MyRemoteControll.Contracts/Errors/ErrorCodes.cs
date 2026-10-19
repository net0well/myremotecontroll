namespace MyRemoteControll.Contracts.Errors;

/// <summary>
/// Códigos de erro estáveis da API e do Hub (catálogo em docs/architecture/hub-contract.md, seção 6).
/// O app decide o comportamento pelo código, nunca pelo texto da mensagem.
/// </summary>
public static class ErrorCodes
{
    public const string AgentPaused = "agent.paused";
    public const string InternalError = "internal.error";
    public const string ValidationFailed = "validation.failed";

    public const string AuthUnauthorized = "auth.unauthorized";
    public const string AuthRevoked = "auth.revoked";
    public const string AuthForbidden = "auth.forbidden";

    public const string SecurityLastAdmin = "security.lastAdmin";
    public const string DeviceNotFound = "devices.notFound";

    public const string PairingInvalidCode = "pairing.invalidCode";
    public const string PairingExpired = "pairing.expired";
    public const string PairingConsumed = "pairing.consumed";
    public const string PairingRejected = "pairing.rejected";
    public const string PairingConfirmationTimeout = "pairing.confirmationTimeout";
    public const string PairingLocked = "pairing.locked";

    public const string PowerAlreadyScheduled = "power.alreadyScheduled";
    public const string PowerActiveWork = "power.activeWork";
    public const string PowerHibernateUnavailable = "power.hibernateUnavailable";
    public const string PowerSleepUnavailable = "power.sleepUnavailable";
    public const string PowerNothingToCancel = "power.nothingToCancel";
}
