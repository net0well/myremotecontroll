using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using MyRemoteControll.Windows.Processes;
using Windows.Win32;
using Windows.Win32.Security;

namespace MyRemoteControll.Windows.Power;

/// <summary>
/// <c>shutdown.exe /t 0</c> para desligar e reiniciar (com <c>/t</c> maior que 0 o Windows força o fechamento dos apps),
/// <c>SetSuspendState</c> para suspender e hibernar, <c>LockWorkStation</c> para bloquear.
/// </summary>
public sealed class WindowsPowerService(IProcessRunner processRunner) : IPowerService
{
    private const string ShutdownExecutable = "shutdown.exe";
    private const int ErrorNotAllAssigned = 1300;

    // Quanto esperar por uma falha imediata do SetSuspendState, que só retorna quando o PC acorda.
    private static readonly TimeSpan SuspendFailureWindow = TimeSpan.FromSeconds(2);

    // Indicadores de reinício pendente por atualização que o Configuration Manager da Microsoft consulta.
    private static readonly string[] RebootPendingKeys =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending",
    ];

    public static IReadOnlyList<string> ShutdownArguments(bool restart, bool force) =>
        force ? [restart ? "/r" : "/s", "/t", "0", "/f"] : [restart ? "/r" : "/s", "/t", "0"];

    public PowerCapabilities GetCapabilities()
    {
        if (!PInvoke.GetPwrCapabilities(out var capabilities))
        {
            return new PowerCapabilities(SleepAvailable: false, HibernateAvailable: false);
        }

        return new PowerCapabilities(
            SleepAvailable: capabilities.SystemS1 || capabilities.SystemS2 || capabilities.SystemS3,
            HibernateAvailable: capabilities.SystemS4 && capabilities.HiberFilePresent);
    }

    public bool IsUpdateRebootPending() =>
        RebootPendingKeys.Any(path =>
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            return key is not null;
        });

    public Task ExecuteAsync(PowerOperation operation, bool force, CancellationToken cancellationToken) =>
        operation switch
        {
            PowerOperation.Shutdown => ShutdownAsync(restart: false, force, cancellationToken),
            PowerOperation.Restart => ShutdownAsync(restart: true, force, cancellationToken),
            PowerOperation.Sleep => SuspendAsync(hibernate: false),
            PowerOperation.Hibernate => SuspendAsync(hibernate: true),
            PowerOperation.Lock => Lock(),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

    private async Task ShutdownAsync(bool restart, bool force, CancellationToken cancellationToken)
    {
        var result = await processRunner
            .RunAsync(ShutdownExecutable, ShutdownArguments(restart, force), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new PowerException($"O shutdown.exe terminou com código {result.ExitCode}: {result.StandardError.Trim()}");
        }
    }

    private static Task Lock() =>
        PInvoke.LockWorkStation()
            ? Task.CompletedTask
            : Task.FromException(new PowerException("O Windows não bloqueou a sessão.", new Win32Exception(Marshal.GetLastPInvokeError())));

    /// <summary>
    /// O <c>SetSuspendState</c> só retorna quando o PC acorda, então roda numa thread própria; uma falha aparece logo
    /// (ex.: sem permissão), e depois disso a suspensão é considerada iniciada.
    /// </summary>
    private static async Task SuspendAsync(bool hibernate)
    {
        var outcome = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                EnableShutdownPrivilege();
                if (PInvoke.SetSuspendState(hibernate, bForce: false, bWakeupEventsDisabled: false))
                {
                    outcome.TrySetResult();
                }
                else
                {
                    outcome.TrySetException(new PowerException(
                        hibernate ? "O Windows não hibernou." : "O Windows não suspendeu.",
                        new Win32Exception(Marshal.GetLastPInvokeError())));
                }
            }
            catch (Exception ex) when (ex is Win32Exception or PowerException)
            {
                outcome.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "MyRemoteControll suspensão",
        };
        thread.Start();

        await Task.WhenAny(outcome.Task, Task.Delay(SuspendFailureWindow)).ConfigureAwait(false);
        if (outcome.Task.IsFaulted)
        {
            await outcome.Task.ConfigureAwait(false);
        }
    }

    /// <summary>O <c>SetSuspendState</c> exige o privilégio de desligar ligado no token do processo.</summary>
    private static unsafe void EnableShutdownPrivilege()
    {
        using var process = Process.GetCurrentProcess();
        if (!PInvoke.OpenProcessToken(process.SafeHandle, TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | TOKEN_ACCESS_MASK.TOKEN_QUERY, out var token))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        using (token)
        {
            if (!PInvoke.LookupPrivilegeValue(null, PInvoke.SE_SHUTDOWN_NAME, out var luid))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            var privileges = new TOKEN_PRIVILEGES { PrivilegeCount = 1 };
            privileges.Privileges[0] = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = TOKEN_PRIVILEGES_ATTRIBUTES.SE_PRIVILEGE_ENABLED };
            if (!PInvoke.AdjustTokenPrivileges(token, false, &privileges, default))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            // Retorna sucesso mesmo sem conceder; o motivo real vem no último erro.
            if (Marshal.GetLastPInvokeError() == ErrorNotAllAssigned)
            {
                throw new PowerException("Este usuário do Windows não tem o direito de desligar o sistema.");
            }
        }
    }
}
