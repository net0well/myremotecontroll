using MyRemoteControll.Agent.Domain.Audit;

namespace MyRemoteControll.Agent.Infrastructure.Audit;

/// <summary>Nomes de ação da auditoria compartilhados entre features.</summary>
internal static class AuditActions
{
    /// <summary>Negação de acesso (REST ou Hub); o alvo traz a operação e a permissão exigida.</summary>
    public const string AccessDenied = "auth.denied";
}

/// <summary>Quem fez a ação: aparelho remoto, bandeja, agendador ou o próprio sistema.</summary>
internal sealed record AuditActor(AuditSource Source, Guid? DeviceId = null, bool LocalConfirmation = false)
{
    public static readonly AuditActor Tray = new(AuditSource.Tray);

    public static AuditActor Device(Guid? deviceId, bool localConfirmation = false) => new(AuditSource.Device, deviceId, localConfirmation);

    public AuditRecord Record(string action, AuditResult result) =>
        new(action, result) { Source = Source, DeviceId = DeviceId, LocalConfirmation = LocalConfirmation };
}
