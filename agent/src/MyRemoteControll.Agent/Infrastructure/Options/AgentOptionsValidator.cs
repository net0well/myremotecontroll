using Microsoft.Extensions.Options;

namespace MyRemoteControll.Agent.Infrastructure.Options;

internal sealed class AgentOptionsValidator : IValidateOptions<AgentOptions>
{
    public ValidateOptionsResult Validate(string? name, AgentOptions options)
    {
        List<string> failures = [];

        if (!IsValidPort(options.PublicPort))
        {
            failures.Add($"Agent:PublicPort deve estar entre 0 e 65535 (valor atual: {options.PublicPort}).");
        }

        if (!IsValidPort(options.LoopbackPort))
        {
            failures.Add($"Agent:LoopbackPort deve estar entre 0 e 65535 (valor atual: {options.LoopbackPort}).");
        }

        if (options.PublicPort != 0 && options.PublicPort == options.LoopbackPort)
        {
            failures.Add($"Agent:PublicPort e Agent:LoopbackPort precisam ser diferentes (ambas são {options.PublicPort}).");
        }

        if (!string.IsNullOrWhiteSpace(options.DataDirectory) && !Path.IsPathFullyQualified(options.DataDirectory))
        {
            failures.Add($"Agent:DataDirectory precisa ser um caminho absoluto (valor atual: '{options.DataDirectory}').");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>Valida antes do host existir; lança <see cref="OptionsValidationException"/> com todas as falhas.</summary>
    public static void ThrowIfInvalid(AgentOptions options)
    {
        var result = new AgentOptionsValidator().Validate(Microsoft.Extensions.Options.Options.DefaultName, options);
        if (result.Failed)
        {
            throw new OptionsValidationException(
                Microsoft.Extensions.Options.Options.DefaultName, typeof(AgentOptions), result.Failures ?? []);
        }
    }

    private static bool IsValidPort(int port) => port is >= 0 and <= 65535;
}
