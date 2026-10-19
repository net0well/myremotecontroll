using Microsoft.Extensions.Options;
using MyRemoteControll.Agent.Infrastructure.Options;

namespace MyRemoteControll.Agent.Tests.Options;

public sealed class AgentOptionsValidatorTests
{
    [Fact]
    public void Defaults_AreValid() => AgentOptionsValidator.ThrowIfInvalid(new AgentOptions());

    [Fact]
    public void SamePortForBothListeners_IsRejectedWithClearMessage()
    {
        var error = Assert.Throws<OptionsValidationException>(
            () => AgentOptionsValidator.ThrowIfInvalid(new AgentOptions { PublicPort = 47800, LoopbackPort = 47800 }));

        Assert.Contains(error.Failures, f => f.Contains("precisam ser diferentes", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(70000)]
    public void PortOutOfRange_IsRejected(int port) =>
        Assert.Throws<OptionsValidationException>(() => AgentOptionsValidator.ThrowIfInvalid(new AgentOptions { PublicPort = port }));

    [Fact]
    public void RelativeDataDirectory_IsRejected() =>
        Assert.Throws<OptionsValidationException>(
            () => AgentOptionsValidator.ThrowIfInvalid(new AgentOptions { DataDirectory = @"dados\agente" }));

    [Fact]
    public void DynamicPorts_AreAllowedForTests() =>
        AgentOptionsValidator.ThrowIfInvalid(new AgentOptions { PublicPort = 0, LoopbackPort = 0 });
}
