namespace MyRemoteControll.Agent.Infrastructure.Options;

/// <summary>Locais de dados do agente (ADR-0007 e ADR-0020).</summary>
public sealed record AgentPaths(string DataDirectory)
{
    public string LogsDirectory => Path.Combine(DataDirectory, "logs");

    public string DatabasePath => Path.Combine(DataDirectory, "data.db");

    public static string DefaultDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyRemoteControll");

    public static AgentPaths From(AgentOptions options) =>
        new(string.IsNullOrWhiteSpace(options.DataDirectory) ? DefaultDataDirectory : options.DataDirectory);
}
