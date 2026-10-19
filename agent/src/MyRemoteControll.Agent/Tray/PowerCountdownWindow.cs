using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using MyRemoteControll.Agent.Features.Power;
using MyRemoteControll.Contracts.Power;

namespace MyRemoteControll.Agent.Tray;

/// <summary>
/// Contagem de uma ação de energia pedida pelo celular (spec 005): fica sempre visível no canto da área de trabalho,
/// sem roubar o foco, com o aparelho de origem e "Cancelar". Fechar a janela só a esconde; o menu da bandeja continua
/// com a opção de cancelar.
/// </summary>
internal sealed class PowerCountdownWindow : Form
{
    private const int ScreenMargin = 12;

    private readonly PowerCountdownNotice _notice;
    private readonly TimeProvider _timeProvider;
    private readonly Label _remaining;
    private readonly Button _cancel;
    private readonly System.Windows.Forms.Timer _ticker = new() { Interval = 250 };
    private readonly Font _titleFont;
    private readonly Font _remainingFont;

    public PowerCountdownWindow(PowerCountdownNotice notice, TimeProvider timeProvider)
    {
        _notice = notice;
        _timeProvider = timeProvider;
        var countdown = notice.Countdown;

        Text = "MyRemoteControll";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        // Fontes criadas aqui são da janela: os controles não as descartam.
        _titleFont = new Font(Font.FontFamily, Font.Size * 1.3f, FontStyle.Bold);
        _remainingFont = new Font(Font.FontFamily, Font.Size * 2.4f, FontStyle.Bold);
        var title = new Label { AutoSize = true, Font = _titleFont, Text = Title(countdown.Action) };
        _remaining = new Label { AutoSize = true, Font = _remainingFont };
        var details = new Label { AutoSize = true, MaximumSize = new Size(360, 0), Text = Details(countdown) };

        _cancel = new Button { Text = "Cancelar", AutoSize = true };
        _cancel.Click += async (_, _) => await CancelAsync();

        var layout = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.AddRange([title, _remaining, details, _cancel]);
        Controls.Add(layout);

        _ticker.Tick += (_, _) => UpdateRemaining();
        UpdateRemaining();
        _notice.Ended += OnNoticeEnded;
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var area = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
        Location = new Point(area.Right - Width - ScreenMargin, area.Bottom - Height - ScreenMargin);
        _ticker.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notice.Ended -= OnNoticeEnded;
            _ticker.Dispose();
            _titleFont.Dispose();
            _remainingFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private static string Title(PowerActionKind action) => action switch
    {
        PowerActionKind.Shutdown => "O PC vai desligar",
        PowerActionKind.Restart => "O PC vai reiniciar",
        PowerActionKind.Sleep => "O PC vai suspender",
        PowerActionKind.Hibernate => "O PC vai hibernar",
        _ => "A sessão vai ser bloqueada",
    };

    private static string Details(PowerCountdownDto countdown)
    {
        List<string> lines = [$"Pedido pelo aparelho \"{countdown.RequestedByDeviceName ?? "desconhecido"}\" pelo MyRemoteControll."];
        if (countdown.Force)
        {
            lines.Add("Os aplicativos abertos serão fechados à força (trabalho não salvo será perdido).");
        }

        if (countdown.UpdatePending)
        {
            lines.Add("Há atualizações do Windows pendentes; pode demorar.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void UpdateRemaining()
    {
        var remaining = _notice.Countdown.ExecutesAt - _timeProvider.GetUtcNow();
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        _remaining.Text = remaining.TotalHours >= 1
            ? remaining.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : remaining.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private async Task CancelAsync()
    {
        _cancel.Enabled = false;
        if (!await _notice.CancelAsync())
        {
            // Já tinha terminado (executada ou cancelada por um aparelho).
            Close();
        }
    }

    // A contagem termina em outra thread (cancelamento, execução ou encerramento do agente).
    private void OnNoticeEnded(object? sender, EventArgs e)
    {
        if (IsHandleCreated && !IsDisposed)
        {
            BeginInvoke(Close);
        }
    }
}
