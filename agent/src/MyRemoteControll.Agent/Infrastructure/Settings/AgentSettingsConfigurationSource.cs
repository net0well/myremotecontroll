namespace MyRemoteControll.Agent.Infrastructure.Settings;

internal sealed class AgentSettingsConfigurationSource(string connectionString) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new AgentSettingsConfigurationProvider(connectionString);
}
