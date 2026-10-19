namespace MyRemoteControll.Windows.Power;

public enum PowerOperation
{
    Shutdown,
    Restart,
    Sleep,
    Hibernate,
    Lock,
}

/// <summary>O que o PC oferece: suspender (S1–S3) e hibernar (S4 com arquivo de hibernação).</summary>
public sealed record PowerCapabilities(bool SleepAvailable, bool HibernateAvailable);

/// <summary>Ações de energia do Windows (spec 005). A contagem fica com o agente; aqui só se executa na hora.</summary>
public interface IPowerService
{
    PowerCapabilities GetCapabilities();

    /// <summary>O Windows Update espera reinício: desligar ou reiniciar pode demorar.</summary>
    bool IsUpdateRebootPending();

    /// <summary>Executa a ação agora. Falha do Windows vira <see cref="PowerException"/>.</summary>
    Task ExecuteAsync(PowerOperation operation, bool force, CancellationToken cancellationToken);
}
