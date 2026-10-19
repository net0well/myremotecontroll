using System.Security.Principal;
using System.Text;
using System.Xml.Linq;
using MyRemoteControll.Windows.Processes;

namespace MyRemoteControll.Windows.Autostart;

/// <summary>
/// Autostart por tarefa do Agendador de Tarefas "ao fazer logon" do usuário atual, sem elevação.
/// O estado é lido pelo código de saída do <c>schtasks</c>, nunca pelo texto (que vem traduzido).
/// </summary>
public sealed class ScheduledTaskAutostart(
    IProcessRunner processRunner,
    string? userId = null,
    string taskName = ScheduledTaskAutostart.DefaultTaskName) : IAutostartManager
{
    public const string DefaultTaskName = "MyRemoteControll Agent";

    private const string SchTasks = "schtasks.exe";
    private static readonly XNamespace TaskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    private readonly string _userId = userId ?? WindowsIdentity.GetCurrent().Name;

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(SchTasks, ["/Query", "/TN", taskName], cancellationToken).ConfigureAwait(false);
        return result.Succeeded;
    }

    public async Task EnableAsync(string executablePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var xmlPath = Path.Combine(Path.GetTempPath(), $"myremotecontroll-task-{Guid.NewGuid():N}.xml");
        try
        {
            // O schtasks exige o XML da tarefa em UTF-16.
            await File.WriteAllTextAsync(xmlPath, BuildTaskXml(_userId, executablePath), Encoding.Unicode, cancellationToken)
                .ConfigureAwait(false);

            var result = await processRunner.RunAsync(SchTasks, ["/Create", "/TN", taskName, "/XML", xmlPath, "/F"], cancellationToken)
                .ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new AutostartException($"Não foi possível criar a tarefa de inicialização: {result.StandardError.Trim()}");
            }
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    public async Task DisableAsync(CancellationToken cancellationToken)
    {
        if (!await IsEnabledAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var result = await processRunner.RunAsync(SchTasks, ["/Delete", "/TN", taskName, "/F"], cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new AutostartException($"Não foi possível remover a tarefa de inicialização: {result.StandardError.Trim()}");
        }
    }

    internal static string BuildTaskXml(string userId, string executablePath)
    {
        var ns = TaskNamespace;
        var document = new XDocument(
            new XElement(ns + "Task",
                new XAttribute("version", "1.2"),
                new XElement(ns + "RegistrationInfo",
                    new XElement(ns + "Description", "Inicia o MyRemoteControll Agent ao fazer logon.")),
                new XElement(ns + "Triggers",
                    new XElement(ns + "LogonTrigger",
                        new XElement(ns + "Enabled", "true"),
                        new XElement(ns + "UserId", userId))),
                new XElement(ns + "Principals",
                    new XElement(ns + "Principal",
                        new XAttribute("id", "Author"),
                        new XElement(ns + "UserId", userId),
                        new XElement(ns + "LogonType", "InteractiveToken"),
                        new XElement(ns + "RunLevel", "LeastPrivilege"))),
                new XElement(ns + "Settings",
                    new XElement(ns + "MultipleInstancesPolicy", "IgnoreNew"),
                    new XElement(ns + "DisallowStartIfOnBatteries", "false"),
                    new XElement(ns + "StopIfGoingOnBatteries", "false"),
                    new XElement(ns + "ExecutionTimeLimit", "PT0S"),
                    new XElement(ns + "RestartOnFailure",
                        new XElement(ns + "Interval", "PT1M"),
                        new XElement(ns + "Count", "3"))),
                new XElement(ns + "Actions",
                    new XAttribute("Context", "Author"),
                    new XElement(ns + "Exec",
                        new XElement(ns + "Command", executablePath)))));

        return document.ToString();
    }
}
