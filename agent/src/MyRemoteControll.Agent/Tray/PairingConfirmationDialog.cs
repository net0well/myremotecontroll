using System.Drawing;
using System.Windows.Forms;
using MyRemoteControll.Agent.Features.Pairing;
using MyRemoteControll.Contracts.Devices;

namespace MyRemoteControll.Agent.Tray;

/// <summary>
/// "Permitir / Recusar" um pareamento (spec 002). Fechar a janela recusa. Não há botão padrão para que um Enter
/// acidental não aprove nada.
/// </summary>
internal sealed class PairingConfirmationDialog : Form
{
    private readonly PairingConfirmationRequest _request;

    public PairingConfirmationDialog(PairingConfirmationRequest request)
    {
        _request = request;
        var info = request.Info;

        Text = "Confirmar pareamento — MyRemoteControll";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        var platform = info.Platform == DevicePlatform.Ios ? "iOS" : "Android";
        var message = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Text = $"O aparelho \"{info.DeviceName}\" ({platform}) quer parear com este PC.\n" +
                   $"Endereço de origem: {info.RemoteAddress ?? "desconhecido"}\n\n" +
                   "Só permita se foi você quem escaneou o código agora.",
        };

        var layout = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.Add(message);
        if (info.ActiveDeviceCount >= 5)
        {
            layout.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(420, 0),
                ForeColor = Color.DarkOrange,
                Text = $"Atenção: já existem {info.ActiveDeviceCount} aparelhos pareados com este PC.",
            });
        }

        var allow = new Button { Text = "Permitir", AutoSize = true };
        allow.Click += (_, _) =>
        {
            _request.Approve();
            Close();
        };
        var reject = new Button { Text = "Recusar", AutoSize = true };
        reject.Click += (_, _) => Close();

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        buttons.Controls.AddRange([allow, reject]);
        layout.Controls.Add(buttons);
        Controls.Add(layout);

        CancelButton = reject;
        FormClosed += (_, _) => _request.Reject();
        _request.Closed += OnRequestClosed;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _request.Closed -= OnRequestClosed;
        }

        base.Dispose(disposing);
    }

    // O pedido pode fechar por tempo esgotado em outra thread.
    private void OnRequestClosed(object? sender, EventArgs e)
    {
        if (IsHandleCreated && !IsDisposed)
        {
            BeginInvoke(Close);
        }
    }
}
