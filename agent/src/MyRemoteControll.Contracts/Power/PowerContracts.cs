namespace MyRemoteControll.Contracts.Power;

public enum PowerActionKind
{
    Shutdown,
    Restart,
    Sleep,
    Hibernate,
    Lock,
}

/// <summary>
/// Pedido de ação de energia (spec 005). <see cref="DelaySeconds"/> de 0 a 86400; <see cref="Force"/> fecha os apps à
/// força no prazo; <see cref="LocalConfirmation"/> diz se o app confirmou com biometria/PIN (vai para a auditoria).
/// </summary>
public sealed record PowerActionRequest(
    PowerActionKind Action,
    int DelaySeconds,
    bool Force,
    bool AcknowledgeActiveWork,
    bool LocalConfirmation);

/// <summary>Contagem ativa, enviada a todos os aparelhos.</summary>
/// <param name="UpdatePending">O Windows tem atualização esperando reinício: desligar ou reiniciar pode demorar.</param>
public sealed record PowerCountdownDto(
    PowerActionKind Action,
    DateTimeOffset ExecutesAt,
    bool Force,
    Guid? RequestedByDeviceId,
    string? RequestedByDeviceName,
    bool UpdatePending);

public enum PowerCancelSource
{
    /// <summary>Um aparelho chamou <c>CancelPowerAction</c>.</summary>
    Device,

    /// <summary>Cancelado no PC (janela de contagem ou menu da bandeja).</summary>
    Tray,

    /// <summary>O agente foi encerrado durante a contagem.</summary>
    AgentShutdown,

    /// <summary>O Windows recusou a ação no prazo.</summary>
    Failed,
}

public sealed record PowerCancelledDto(PowerActionKind Action, Guid? CancelledByDeviceId, PowerCancelSource Source);

/// <summary>Trabalho que seria interrompido (sessões do Claude, builds), listado em <c>power.activeWork</c>.</summary>
public sealed record ActiveWorkItemDto(string Kind, string Description);
