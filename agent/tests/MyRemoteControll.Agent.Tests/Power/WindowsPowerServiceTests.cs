using MyRemoteControll.Agent.Features.Power;
using MyRemoteControll.Contracts.Power;
using MyRemoteControll.Windows.Power;
using MyRemoteControll.Windows.Processes;

namespace MyRemoteControll.Agent.Tests.Power;

public sealed class WindowsPowerServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(PowerOperation.Shutdown, false, new[] { "/s", "/t", "0" })]
    [InlineData(PowerOperation.Shutdown, true, new[] { "/s", "/t", "0", "/f" })]
    [InlineData(PowerOperation.Restart, false, new[] { "/r", "/t", "0" })]
    [InlineData(PowerOperation.Restart, true, new[] { "/r", "/t", "0", "/f" })]
    public async Task ShutdownAndRestart_RunShutdownExeNowAndForceOnlyWhenAsked(PowerOperation operation, bool force, string[] expected)
    {
        // Com /t maior que 0 o Windows implica /f; por isso a contagem é do agente e aqui o prazo é sempre 0.
        var runner = new RecordingRunner();
        var power = new WindowsPowerService(runner);

        await power.ExecuteAsync(operation, force, Ct);

        var call = Assert.Single(runner.Calls);
        Assert.Equal("shutdown.exe", call.FileName);
        Assert.Equal(expected, call.Arguments);
    }

    [Fact]
    public async Task ShutdownExeFailure_BecomesPowerExceptionWithTheErrorOutput()
    {
        var runner = new RecordingRunner { Result = new ProcessResult(1190, "", "Já existe um desligamento agendado.") };
        var power = new WindowsPowerService(runner);

        var error = await Assert.ThrowsAsync<PowerException>(() => power.ExecuteAsync(PowerOperation.Shutdown, force: false, Ct));

        Assert.Contains("1190", error.Message, StringComparison.Ordinal);
        Assert.Contains("desligamento agendado", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CapabilitiesAndPendingReboot_AreReadWithoutChangingTheSystem()
    {
        var power = new WindowsPowerService(new RecordingRunner());

        var capabilities = power.GetCapabilities();
        _ = power.IsUpdateRebootPending();

        Assert.NotNull(capabilities);
    }

    [Theory]
    [InlineData(PowerActionKind.Shutdown, "power.shutdown")]
    [InlineData(PowerActionKind.Restart, "power.shutdown")]
    [InlineData(PowerActionKind.Hibernate, "power.shutdown")]
    [InlineData(PowerActionKind.Sleep, "power.basic")]
    [InlineData(PowerActionKind.Lock, "power.basic")]
    public void RequiredPermission_FollowsTheSection9Matrix(PowerActionKind action, string permission) =>
        Assert.Equal(permission, PowerService.RequiredPermission(action));

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(86_400, true)]
    [InlineData(86_401, false)]
    public void Validator_AcceptsDelaysFromZeroTo24Hours(int delaySeconds, bool valid)
    {
        var result = new PowerActionRequestValidator().Validate(new PowerActionRequest(PowerActionKind.Lock, delaySeconds, false, false, false));

        Assert.Equal(valid, result.IsValid);
    }

    private sealed class RecordingRunner : IProcessRunner
    {
        public List<(string FileName, string[] Arguments)> Calls { get; } = [];

        public ProcessResult Result { get; init; } = new(0, "", "");

        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Calls.Add((fileName, [.. arguments]));
            return Task.FromResult(Result);
        }
    }
}
