using System.Xml.Linq;
using MyRemoteControll.Windows.Autostart;
using MyRemoteControll.Windows.Processes;

namespace MyRemoteControll.Agent.Tests.Autostart;

public sealed class ScheduledTaskAutostartTests
{
    private static readonly XNamespace Task = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    [Fact]
    public void AC1_AutostartTask_UsesLogonTriggerForCurrentUserWithoutElevation()
    {
        const string user = @"PC\usuario";
        const string executable = @"C:\Program Files\MyRemoteControll\MyRemoteControll.Agent.exe";

        var xml = XDocument.Parse(ScheduledTaskAutostart.BuildTaskXml(user, executable));

        var trigger = xml.Descendants(Task + "LogonTrigger").Single();
        Assert.Equal(user, trigger.Element(Task + "UserId")?.Value);
        var principal = xml.Descendants(Task + "Principal").Single();
        Assert.Equal(user, principal.Element(Task + "UserId")?.Value);
        Assert.Equal("LeastPrivilege", principal.Element(Task + "RunLevel")?.Value);
        Assert.Equal("InteractiveToken", principal.Element(Task + "LogonType")?.Value);
        Assert.Equal(executable, xml.Descendants(Task + "Command").Single().Value);
    }

    [Fact]
    public async Task AC9_DisablingAutostart_DeletesTheTask()
    {
        var runner = new FakeSchTasks(taskExists: true);
        var autostart = new ScheduledTaskAutostart(runner, userId: @"PC\usuario");

        await autostart.DisableAsync(TestContext.Current.CancellationToken);

        Assert.Contains(runner.Calls, call => call is ["/Delete", "/TN", ScheduledTaskAutostart.DefaultTaskName, "/F"]);
        Assert.False(await autostart.IsEnabledAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DisablingAutostart_WhenNoTaskExists_DoesNotCallDelete()
    {
        var runner = new FakeSchTasks(taskExists: false);
        var autostart = new ScheduledTaskAutostart(runner, userId: @"PC\usuario");

        await autostart.DisableAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(runner.Calls, call => call[0] == "/Delete");
    }

    [Fact]
    public async Task EnablingAutostart_WhenSchTasksFails_ThrowsWithTheErrorOutput()
    {
        var runner = new FakeSchTasks(taskExists: false) { CreateExitCode = 1, CreateError = "ERRO: acesso negado." };
        var autostart = new ScheduledTaskAutostart(runner, userId: @"PC\usuario");

        var error = await Assert.ThrowsAsync<AutostartException>(
            () => autostart.EnableAsync(@"C:\agente.exe", TestContext.Current.CancellationToken));

        Assert.Contains("acesso negado", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Cria e remove uma tarefa real no Agendador de Tarefas; rode manualmente.</summary>
    [Fact(Explicit = true)]
    public async Task System_RealScheduledTask_CanBeCreatedAndRemovedWithoutElevation()
    {
        var autostart = new ScheduledTaskAutostart(new ProcessRunner(), taskName: $"MyRemoteControll Teste {Guid.NewGuid():N}");
        var ct = TestContext.Current.CancellationToken;
        try
        {
            await autostart.EnableAsync(@"C:\Windows\System32\notepad.exe", ct);
            Assert.True(await autostart.IsEnabledAsync(ct));
        }
        finally
        {
            await autostart.DisableAsync(ct);
        }

        Assert.False(await autostart.IsEnabledAsync(ct));
    }

    private sealed class FakeSchTasks(bool taskExists) : IProcessRunner
    {
        private bool _taskExists = taskExists;

        public List<string[]> Calls { get; } = [];

        public int CreateExitCode { get; init; }

        public string CreateError { get; init; } = "";

        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Calls.Add([.. arguments]);
            var result = arguments[0] switch
            {
                "/Query" => new ProcessResult(_taskExists ? 0 : 1, "", ""),
                "/Create" => new ProcessResult(CreateExitCode, "", CreateError),
                "/Delete" => Delete(),
                _ => throw new InvalidOperationException($"Argumento inesperado: {arguments[0]}"),
            };
            return System.Threading.Tasks.Task.FromResult(result);
        }

        private ProcessResult Delete()
        {
            _taskExists = false;
            return new ProcessResult(0, "", "");
        }
    }
}
