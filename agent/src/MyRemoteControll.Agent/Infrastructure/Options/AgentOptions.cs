namespace MyRemoteControll.Agent.Infrastructure.Options;

/// <summary>Configuração do host (seção <c>Agent</c> do appsettings.json).</summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Porta HTTPS do listener público (LAN/Tailscale). Zero escolhe uma porta livre (uso em testes).</summary>
    public int PublicPort { get; set; } = 47800;

    /// <summary>Porta HTTP do listener loopback (MCP e hooks do Claude). Zero escolhe uma porta livre (uso em testes).</summary>
    public int LoopbackPort { get; set; } = 47801;

    /// <summary>Diretório de dados; vazio usa <c>%LOCALAPPDATA%\MyRemoteControll</c>.</summary>
    public string? DataDirectory { get; set; }
}
