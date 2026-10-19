namespace MyRemoteControll.Windows.Autostart;

/// <summary>Liga e desliga a inicialização do agente no logon do usuário (ADR-0003).</summary>
public interface IAutostartManager
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken);

    Task EnableAsync(string executablePath, CancellationToken cancellationToken);

    Task DisableAsync(CancellationToken cancellationToken);
}
