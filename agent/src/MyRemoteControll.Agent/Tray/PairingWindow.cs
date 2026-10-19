using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using MyRemoteControll.Agent.Features.Pairing;
using QRCoder;

namespace MyRemoteControll.Agent.Tray;

/// <summary>Janela "Parear aparelho": QR com o código de uso único, contagem regressiva e o link para digitar à mão.</summary>
internal sealed class PairingWindow : Form
{
    private const int QrPixelsPerModule = 8;

    private readonly PairingService _pairing;
    private readonly TimeProvider _timeProvider;
    private readonly PictureBox _qr;
    private readonly Label _countdown;
    private readonly TextBox _link;
    private readonly Button _generate;
    private readonly System.Windows.Forms.Timer _timer;
    private PairingOffer? _offer;

    public PairingWindow(PairingService pairing, TimeProvider timeProvider)
    {
        _pairing = pairing;
        _timeProvider = timeProvider;

        Text = "Parear aparelho — MyRemoteControll";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        var instructions = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(340, 0),
            Text = "No celular, abra o MyRemoteControll e escaneie o código. Depois, confirme o pareamento aqui no PC.",
        };
        _qr = new PictureBox { Size = new Size(340, 340), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
        _countdown = new Label { AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        var linkLabel = new Label { AutoSize = true, Text = "Sem câmera? Copie o link e cole no app:" };
        _link = new TextBox { ReadOnly = true, Width = 340 };
        var copy = new Button { Text = "Copiar link", AutoSize = true };
        copy.Click += (_, _) =>
        {
            if (_link.Text.Length > 0)
            {
                Clipboard.SetText(_link.Text);
            }
        };
        _generate = new Button { Text = "Gerar novo código", AutoSize = true };
        _generate.Click += async (_, _) => await GenerateAsync();
        var close = new Button { Text = "Fechar", AutoSize = true, DialogResult = DialogResult.Cancel };
        CancelButton = close;

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        buttons.Controls.AddRange([copy, _generate, close]);

        var layout = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.AddRange([instructions, _qr, _countdown, linkLabel, _link, buttons]);
        Controls.Add(layout);

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => UpdateCountdown();
        Load += async (_, _) => await GenerateAsync();
        FormClosed += (_, _) => _timer.Stop();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _qr.Image?.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task GenerateAsync()
    {
        _generate.Enabled = false;
        try
        {
            _offer = await _pairing.CreateOfferAsync(CancellationToken.None);
            _qr.Image?.Dispose();
            _qr.Image = RenderQr(_offer.Payload);
            _link.Text = _offer.Payload;
            _timer.Start();
            UpdateCountdown();
        }
        finally
        {
            _generate.Enabled = true;
        }
    }

    private void UpdateCountdown()
    {
        if (_offer is null)
        {
            return;
        }

        var remaining = _offer.ExpiresAt - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            _timer.Stop();
            _countdown.Text = "Código expirado — clique em \"Gerar novo código\".";
            _qr.Enabled = false;
            return;
        }

        _qr.Enabled = true;
        _countdown.Text = string.Create(CultureInfo.InvariantCulture, $"Expira em {(int)remaining.TotalMinutes}:{remaining.Seconds:00}");
    }

    private static Bitmap RenderQr(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(QrPixelsPerModule);
        using var stream = new MemoryStream(png);
        using var decoded = new Bitmap(stream);

        // Bitmap criado de um stream exige o stream aberto pela vida toda; a cópia não.
        return new Bitmap(decoded);
    }
}
