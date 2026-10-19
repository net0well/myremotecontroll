using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using MyRemoteControll.Agent.Features.Devices;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Contracts.Devices;

namespace MyRemoteControll.Agent.Tray;

/// <summary>Bandeja → Aparelhos (spec 003): listar, renomear, mudar papel e revogar, com as mesmas regras do app.</summary>
internal sealed class DevicesWindow : Form
{
    private static readonly (DeviceRole Role, string Label)[] Roles =
    [
        (DeviceRole.ReadOnly, "Somente leitura"),
        (DeviceRole.Operator, "Operador"),
        (DeviceRole.Administrator, "Administrador"),
    ];

    private readonly DeviceManagement _devices;
    private readonly ListView _list;
    private readonly ComboBox _role;
    private IReadOnlyList<DeviceDto> _current = [];

    public DevicesWindow(DeviceManagement devices)
    {
        _devices = devices;

        Text = "Aparelhos — MyRemoteControll";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(820, 420);
        MinimumSize = new Size(640, 300);

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
        };
        _list.Columns.Add("Nome", 200);
        _list.Columns.Add("Plataforma", 90);
        _list.Columns.Add("Papel", 120);
        _list.Columns.Add("Último acesso", 140);
        _list.Columns.Add("Conectado", 80);
        _list.Columns.Add("Situação", 90);
        _list.SelectedIndexChanged += (_, _) => SyncRoleSelector();

        _role = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        _role.Items.AddRange([.. Roles.Select(r => r.Label)]);

        var applyRole = new Button { Text = "Aplicar papel", AutoSize = true };
        applyRole.Click += async (_, _) => await ApplyRoleAsync();
        var rename = new Button { Text = "Renomear…", AutoSize = true };
        rename.Click += async (_, _) => await RenameAsync();
        var revoke = new Button { Text = "Revogar", AutoSize = true };
        revoke.Click += async (_, _) => await RevokeAsync();
        var refresh = new Button { Text = "Atualizar", AutoSize = true };
        refresh.Click += async (_, _) => await LoadDevicesAsync();

        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        actions.Controls.AddRange([new Label { Text = "Papel:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, _role, applyRole, rename, revoke, refresh]);

        Controls.Add(_list);
        Controls.Add(actions);
        Load += async (_, _) => await LoadDevicesAsync();
    }

    private DeviceDto? Selected =>
        _list.SelectedItems.Count == 1 ? _list.SelectedItems[0].Tag as DeviceDto : null;

    private async Task LoadDevicesAsync()
    {
        var selectedId = Selected?.Id;
        _current = await _devices.ListAsync(CancellationToken.None);
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var device in _current)
        {
            var item = new ListViewItem(device.Name) { Tag = device, ForeColor = device.Revoked ? SystemColors.GrayText : SystemColors.WindowText };
            item.SubItems.Add(device.Platform == DevicePlatform.Ios ? "iOS" : "Android");
            item.SubItems.Add(RoleLabel(device.Role));
            item.SubItems.Add(device.LastSeenAt?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "—");
            item.SubItems.Add(device.Connected ? "sim" : "não");
            item.SubItems.Add(device.Revoked ? "Revogado" : "Ativo");
            item.Selected = device.Id == selectedId;
            _list.Items.Add(item);
        }

        _list.EndUpdate();
        SyncRoleSelector();
    }

    private void SyncRoleSelector()
    {
        _role.Enabled = Selected is { Revoked: false };
        if (Selected is { } device)
        {
            _role.SelectedIndex = Array.FindIndex(Roles, r => r.Role == device.Role);
        }
    }

    private async Task ApplyRoleAsync()
    {
        if (Selected is not { Revoked: false } device || _role.SelectedIndex < 0)
        {
            return;
        }

        var role = Roles[_role.SelectedIndex].Role;
        await UpdateAsync(device, new UpdateDevicePermissionsRequest(role, device.ExtraPermissions));
    }

    private async Task RenameAsync()
    {
        if (Selected is not { Revoked: false } device)
        {
            return;
        }

        using var prompt = new TextPrompt("Renomear aparelho", "Novo nome:", device.Name);
        if (prompt.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(prompt.Value))
        {
            await UpdateAsync(device, new UpdateDevicePermissionsRequest(device.Role, device.ExtraPermissions, prompt.Value));
        }
    }

    private async Task UpdateAsync(DeviceDto device, UpdateDevicePermissionsRequest request)
    {
        var result = await _devices.UpdatePermissionsAsync(device.Id, request, AuditActor.Tray, CancellationToken.None);
        if (!result.IsSuccess)
        {
            MessageBox.Show(this, result.Error!.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        await LoadDevicesAsync();
    }

    private async Task RevokeAsync()
    {
        if (Selected is not { Revoked: false } device)
        {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Revogar \"{device.Name}\"? O aparelho será desconectado e precisará parear de novo.",
            Text,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        var result = await _devices.RevokeAsync(device.Id, AuditActor.Tray, CancellationToken.None);
        if (!result.IsSuccess)
        {
            MessageBox.Show(this, result.Error!.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        await LoadDevicesAsync();
    }

    private static string RoleLabel(DeviceRole role) => Roles.First(r => r.Role == role).Label;
}

/// <summary>Caixa de texto simples (o WinForms não tem um InputBox).</summary>
internal sealed class TextPrompt : Form
{
    private readonly TextBox _input;

    public TextPrompt(string title, string label, string initialValue)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        _input = new TextBox { Text = initialValue, Width = 300, MaxLength = 100 };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true };
        AcceptButton = ok;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange([ok, cancel]);
        var layout = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.AddRange([new Label { Text = label, AutoSize = true }, _input, buttons]);
        Controls.Add(layout);
    }

    public string Value => _input.Text.Trim();
}
