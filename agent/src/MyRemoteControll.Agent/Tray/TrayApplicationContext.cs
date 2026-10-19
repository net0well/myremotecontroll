using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using MyRemoteControll.Agent.Features.Pairing;
using MyRemoteControll.Agent.Features.Power;
using MyRemoteControll.Agent.Hosting;
using MyRemoteControll.Agent.Infrastructure.Options;
using MyRemoteControll.Contracts.Devices;
using MyRemoteControll.Contracts.Power;
using MyRemoteControll.Windows.Autostart;

namespace MyRemoteControll.Agent.Tray;

/// <summary>Ícone e menu da bandeja (spec 001). Eventos do runtime chegam em outras threads e são levados à UI.</summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int TooltipMaxLength = 127;
    private static readonly TimeSpan SessionEndingStopTimeout = TimeSpan.FromSeconds(4);

    private readonly AgentRuntime _runtime;
    private readonly IAutostartManager _autostart;
    private readonly CancellationTokenSource _lifetime;
    private readonly SynchronizationContext _ui;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _pairItem;
    private readonly ToolStripMenuItem _devicesItem;
    private readonly ToolStripMenuItem _auditItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly Dictionary<AgentStatus, Icon> _icons;
    private bool _exiting;
    private string? _lastFaultShown;
    private PairingWindow? _pairingWindow;

    public TrayApplicationContext(
        AgentRuntime runtime,
        IAutostartManager autostart,
        PairingConfirmationBroker pairingConfirmation,
        PowerCountdownBroker powerCountdowns,
        CancellationTokenSource lifetime)
    {
        _runtime = runtime;
        _autostart = autostart;
        _lifetime = lifetime;
        _icons = Enum.GetValues<AgentStatus>().ToDictionary(status => status, TrayIconFactory.Create);

        _statusItem = new ToolStripMenuItem { Enabled = false };
        _pairItem = new ToolStripMenuItem("Parear aparelho…", null, (_, _) => OpenPairingWindow());
        _devicesItem = new ToolStripMenuItem("Aparelhos…", null, (_, _) => OpenWindow<DevicesWindow>(
            services => new DevicesWindow(services.GetRequiredService<Features.Devices.DeviceManagement>())));
        _auditItem = new ToolStripMenuItem("Auditoria…", null, (_, _) => OpenWindow<AuditWindow>(
            services => new AuditWindow(
                services.GetRequiredService<Features.Audit.AuditQuery>(),
                services.GetRequiredService<TimeProvider>())));
        _pauseItem = new ToolStripMenuItem("Pausar acesso remoto", null, (_, _) => TogglePause());
        _autostartItem = new ToolStripMenuItem("Iniciar com o Windows", null, async (_, _) => await ToggleAutostartAsync());
        _menu = new ContextMenuStrip();
        _menu.Items.AddRange(
        [
            _statusItem,
            new ToolStripSeparator(),
            _pairItem,
            _devicesItem,
            _auditItem,
            _pauseItem,
            _autostartItem,
            new ToolStripMenuItem("Abrir logs", null, (_, _) => OpenLogs()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Sair", null, async (_, _) => await ExitAsync()),
        ]);
        _menu.Opening += async (_, _) => await RefreshAutostartAsync();

        // Criar o ContextMenuStrip instala o WindowsFormsSynchronizationContext nesta thread.
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _notifyIcon = new NotifyIcon { ContextMenuStrip = _menu, Visible = true };

        _runtime.StatusChanged += (_, _) => _ui.Post(_ => UpdateStatus(), null);
        _runtime.NoticeRaised += (_, notice) => _ui.Post(_ => ShowBalloon(notice.Title, notice.Message, ToolTipIcon.Warning), null);
        pairingConfirmation.Requested += (_, request) => _ui.Post(_ => ShowPairingConfirmation(request), null);
        powerCountdowns.Started += (_, notice) => _ui.Post(_ => ShowPowerCountdown(notice), null);
        SystemEvents.SessionEnding += OnSessionEnding;

        UpdateStatus();
    }

    /// <summary>Chamado quando outra instância tenta abrir: mostra o menu desta.</summary>
    public void ShowMenu() => _ui.Post(_ => _menu.Show(Cursor.Position), null);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.SessionEnding -= OnSessionEnding;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            foreach (var icon in _icons.Values)
            {
                icon.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void UpdateStatus()
    {
        if (_exiting)
        {
            return;
        }

        var status = _runtime.Status;
        var text = status switch
        {
            AgentStatus.Starting => "Iniciando…",
            AgentStatus.Running => $"Online — porta {_runtime.Endpoints?.PublicPort}",
            AgentStatus.Paused => "Acesso remoto pausado",
            AgentStatus.Faulted => _runtime.StatusMessage ?? "Erro",
            _ => "Encerrado",
        };

        _statusItem.Text = text;
        _notifyIcon.Icon = _icons[status];
        _notifyIcon.Text = Truncate($"MyRemoteControll — {text}");
        _pauseItem.Text = _runtime.Gate.IsPaused ? "Retomar acesso remoto" : "Pausar acesso remoto";
        _pauseItem.Enabled = status is AgentStatus.Running or AgentStatus.Paused;

        // Com o acesso pausado o celular nem alcança o endpoint de pareamento.
        _pairItem.Enabled = status == AgentStatus.Running;
        _devicesItem.Enabled = status is AgentStatus.Running or AgentStatus.Paused;
        _auditItem.Enabled = status is AgentStatus.Running or AgentStatus.Paused;

        // As novas tentativas (ex.: porta ocupada) repetem a mesma falha a cada 30 s: avisa só quando a mensagem muda.
        if (status == AgentStatus.Faulted && text != _lastFaultShown)
        {
            ShowBalloon("MyRemoteControll com problema", text, ToolTipIcon.Error);
        }

        if (status == AgentStatus.Running)
        {
            _lastFaultShown = null;
        }
        else if (status == AgentStatus.Faulted)
        {
            _lastFaultShown = text;
        }
    }

    private void OpenPairingWindow()
    {
        if (_pairingWindow is { IsDisposed: false })
        {
            _pairingWindow.Activate();
            return;
        }

        if (_runtime.Services is not { } services)
        {
            return;
        }

        var pairing = services.GetRequiredService<PairingService>();
        var window = new PairingWindow(pairing, services.GetRequiredService<TimeProvider>());
        void OnPaired(object? sender, DevicePairedEventArgs e) => _ui.Post(
            _ =>
            {
                ShowBalloon("Aparelho pareado", $"\"{e.DeviceName}\" foi pareado como {RoleName(e.Role)}.", ToolTipIcon.Info);
                window.Close();
            },
            null);

        pairing.DevicePaired += OnPaired;
        window.FormClosed += (_, _) =>
        {
            pairing.DevicePaired -= OnPaired;
            _pairingWindow = null;
            window.Dispose();
        };
        _pairingWindow = window;
        window.Show();
        window.Activate();
    }

    /// <summary>Abre uma janela por tipo; se já estiver aberta, só a traz para frente.</summary>
    private void OpenWindow<TWindow>(Func<IServiceProvider, TWindow> create)
        where TWindow : Form
    {
        var existing = Application.OpenForms.OfType<TWindow>().FirstOrDefault();
        if (existing is not null)
        {
            existing.Activate();
            return;
        }

        if (_runtime.Services is not { } services)
        {
            return;
        }

        var window = create(services);
        window.FormClosed += (_, _) => window.Dispose();
        window.Show();
        window.Activate();
    }

    private static void ShowPairingConfirmation(PairingConfirmationRequest request)
    {
        if (request.IsClosed)
        {
            return;
        }

        var dialog = new PairingConfirmationDialog(request);
        dialog.FormClosed += (_, _) => dialog.Dispose();
        dialog.Show();
        dialog.Activate();
    }

    /// <summary>Janela de contagem + item "Cancelar …" no topo do menu, enquanto a contagem existir (spec 005).</summary>
    private void ShowPowerCountdown(PowerCountdownNotice notice)
    {
        if (notice.IsEnded)
        {
            return;
        }

        var timeProvider = _runtime.Services?.GetService<TimeProvider>() ?? TimeProvider.System;
        var window = new PowerCountdownWindow(notice, timeProvider);
        window.FormClosed += (_, _) => window.Dispose();
        window.Show();

        var cancelItem = new ToolStripMenuItem(CancelLabel(notice.Countdown.Action), null, async (_, _) => await notice.CancelAsync());
        _menu.Items.Insert(0, cancelItem);
        notice.Ended += (_, _) => _ui.Post(_ =>
        {
            _menu.Items.Remove(cancelItem);
            cancelItem.Dispose();
        }, null);

        // Pode ter terminado entre a criação e a assinatura do evento.
        if (notice.IsEnded)
        {
            _menu.Items.Remove(cancelItem);
            cancelItem.Dispose();
            window.Close();
        }
    }

    private static string CancelLabel(PowerActionKind action) => action switch
    {
        PowerActionKind.Shutdown => "Cancelar desligamento",
        PowerActionKind.Restart => "Cancelar reinício",
        PowerActionKind.Sleep => "Cancelar suspensão",
        PowerActionKind.Hibernate => "Cancelar hibernação",
        _ => "Cancelar bloqueio",
    };

    private static string RoleName(DeviceRole role) =>
        role switch
        {
            DeviceRole.Administrator => "administrador",
            DeviceRole.Operator => "operador",
            _ => "somente leitura",
        };

    private void TogglePause()
    {
        if (_runtime.Gate.IsPaused)
        {
            _runtime.Gate.Resume();
        }
        else
        {
            _runtime.Gate.Pause();
        }
    }

    private async Task RefreshAutostartAsync()
    {
        try
        {
            _autostartItem.Checked = await _autostart.IsEnabledAsync(_lifetime.Token);
        }
        catch (Exception ex) when (ex is AutostartException or IOException or OperationCanceledException)
        {
            _autostartItem.Checked = false;
        }
    }

    private async Task ToggleAutostartAsync()
    {
        try
        {
            if (await _autostart.IsEnabledAsync(_lifetime.Token))
            {
                await _autostart.DisableAsync(_lifetime.Token);
            }
            else
            {
                var executable = Environment.ProcessPath
                    ?? throw new AutostartException("Não foi possível descobrir o caminho do executável do agente.");
                await _autostart.EnableAsync(executable, _lifetime.Token);
            }

            await RefreshAutostartAsync();
        }
        catch (Exception ex) when (ex is AutostartException or IOException)
        {
            ShowBalloon("Iniciar com o Windows", ex.Message, ToolTipIcon.Error);
        }
    }

    private void OpenLogs()
    {
        var directory = _runtime.LogsDirectory ?? new AgentPaths(AgentPaths.DefaultDataDirectory).LogsDirectory;
        Directory.CreateDirectory(directory);
        using var _ = Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { directory } });
    }

    private async Task ExitAsync()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _statusItem.Text = "Encerrando…";
        await _lifetime.CancelAsync();
        await _runtime.StopAsync(CancellationToken.None);
        ExitThread();
    }

    // Logoff ou desligamento do Windows: o sistema só espera enquanto este handler não retorna, por isso ele bloqueia
    // (com limite de tempo) até o host parar; Task.Run evita depender do contexto da UI.
    private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        _exiting = true;
        _lifetime.Cancel();
        Task.Run(() => _runtime.StopAsync(CancellationToken.None)).Wait(SessionEndingStopTimeout);
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon) =>
        _notifyIcon.ShowBalloonTip(5000, title, text, icon);

    private static string Truncate(string text) =>
        text.Length <= TooltipMaxLength ? text : string.Concat(text.AsSpan(0, TooltipMaxLength - 1), "…");
}
