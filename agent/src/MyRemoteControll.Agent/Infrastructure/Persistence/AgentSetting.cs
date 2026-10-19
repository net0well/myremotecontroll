namespace MyRemoteControll.Agent.Infrastructure.Persistence;

/// <summary>Configuração persistida que se sobrepõe ao appsettings.json (chave no formato <c>Secao:Chave</c>).</summary>
public sealed class AgentSetting
{
    public required string Key { get; set; }

    public required string ValueJson { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
