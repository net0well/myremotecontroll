using MyRemoteControll.Agent.Features.Pairing;
using MyRemoteControll.Agent.Features.Power;
using MyRemoteControll.Agent.Infrastructure.Settings;
using MyRemoteControll.Agent.Infrastructure.Tls;

namespace MyRemoteControll.Agent.Hosting;

/// <summary>Pontos de extensão do runtime; em produção ficam nos padrões, nos testes isolam disco, portas e relógio.</summary>
internal sealed class AgentRuntimeOptions
{
    public string[] Args { get; init; } = [];

    /// <summary>Sobrepõe a configuração (ex.: <c>Agent:DataDirectory</c> temporário e portas 0 nos testes).</summary>
    public IReadOnlyDictionary<string, string?> ConfigurationOverrides { get; init; } = new Dictionary<string, string?>();

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>Nulo usa o repositório de certificados do usuário (<see cref="StoreCertificateProvider"/>).</summary>
    public Func<CertificateProviderContext, ICertificateProvider>? CertificateProviderFactory { get; init; }

    public TimeSpan PortRetryInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Quem confirma pareamentos no PC: a bandeja assina em produção; os testes assinam com respostas fixas.</summary>
    public PairingConfirmationBroker PairingConfirmation { get; init; } = new();

    /// <summary>Avisa o PC das contagens de energia (janela de contagem e menu da bandeja; nos testes, um assinante).</summary>
    public PowerCountdownBroker PowerCountdowns { get; init; } = new();

    /// <summary>Ajustes de serviço depois do registro padrão (nos testes: energia falsa, fontes de trabalho ativo).</summary>
    public Action<IServiceCollection>? ConfigureServices { get; init; }
}

internal sealed record CertificateProviderContext(IConfiguration Configuration, AgentSettingsStore SettingsStore, ILoggerFactory LoggerFactory);
