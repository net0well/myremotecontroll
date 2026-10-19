namespace MyRemoteControll.Agent.Hosting;

public enum AgentStatus
{
    Starting,
    Running,
    Paused,
    Faulted,
    Stopped,
}

/// <summary>Aviso para o usuário na bandeja (ex.: banco recuperado, certificado regenerado).</summary>
public sealed record AgentNotice(string Title, string Message);

/// <summary>Portas efetivamente abertas (diferem da configuração quando ela pede porta 0).</summary>
public sealed record AgentEndpoints(int PublicPort, int LoopbackPort);
