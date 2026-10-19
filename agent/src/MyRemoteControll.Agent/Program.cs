using System.Windows.Forms;
using MyRemoteControll.Agent.Features.Pairing;
using MyRemoteControll.Agent.Features.Power;
using MyRemoteControll.Agent.Hosting;
using MyRemoteControll.Agent.Tray;
using MyRemoteControll.Windows.Autostart;
using MyRemoteControll.Windows.Processes;

namespace MyRemoteControll.Agent;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        using var instance = SingleInstanceGuard.TryAcquire();
        if (instance is null)
        {
            SingleInstanceGuard.SignalExisting();
            return 0;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var lifetime = new CancellationTokenSource();
        var pairingConfirmation = new PairingConfirmationBroker();
        var powerCountdowns = new PowerCountdownBroker();
        var runtime = new AgentRuntime(new AgentRuntimeOptions
        {
            Args = args,
            PairingConfirmation = pairingConfirmation,
            PowerCountdowns = powerCountdowns,
        });
        using (var tray = new TrayApplicationContext(
                   runtime, new ScheduledTaskAutostart(new ProcessRunner()), pairingConfirmation, powerCountdowns, lifetime))
        {
            instance.OnSignal(tray.ShowMenu);

            // O host sobe fora da thread da UI: o ícone aparece na hora e mostra "Iniciando…".
            _ = Task.Run(() => runtime.StartAsync(lifetime.Token));
            Application.Run(tray);
        }

        // O Main precisa ser síncrono por causa do [STAThread] do WinForms; o descarte final (flush dos logs) roda
        // no pool de threads para não depender do contexto da UI, que já terminou.
        Task.Run(() => runtime.DisposeAsync().AsTask()).GetAwaiter().GetResult();
        return 0;
    }
}
