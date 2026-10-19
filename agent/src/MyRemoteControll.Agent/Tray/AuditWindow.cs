using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using MyRemoteControll.Agent.Features.Audit;

namespace MyRemoteControll.Agent.Tray;

/// <summary>Bandeja → Auditoria (spec 003): últimos registros e exportação em CSV.</summary>
internal sealed class AuditWindow : Form
{
    private const int RecentEntries = AuditQuery.MaxPageSize;

    private readonly AuditQuery _audit;
    private readonly TimeProvider _timeProvider;
    private readonly ListView _list;

    public AuditWindow(AuditQuery audit, TimeProvider timeProvider)
    {
        _audit = audit;
        _timeProvider = timeProvider;

        Text = "Auditoria — MyRemoteControll";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(900, 480);
        MinimumSize = new Size(640, 300);

        _list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
        _list.Columns.Add("Data e hora", 140);
        _list.Columns.Add("Origem", 80);
        _list.Columns.Add("Aparelho", 160);
        _list.Columns.Add("Ação", 200);
        _list.Columns.Add("Resultado", 90);
        _list.Columns.Add("Código", 180);

        var export = new Button { Text = "Exportar CSV…", AutoSize = true };
        export.Click += async (_, _) => await ExportAsync();
        var refresh = new Button { Text = "Atualizar", AutoSize = true };
        refresh.Click += async (_, _) => await LoadEntriesAsync();
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        actions.Controls.AddRange([
            new Label { Text = $"Últimos {RecentEntries} registros.", AutoSize = true, Margin = new Padding(3, 8, 12, 3) },
            export,
            refresh,
        ]);

        Controls.Add(_list);
        Controls.Add(actions);
        Load += async (_, _) => await LoadEntriesAsync();
    }

    private async Task LoadEntriesAsync()
    {
        var page = await _audit.PageAsync(new AuditFilter(null, null, null, null, null), cursor: null, RecentEntries, CancellationToken.None);
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var entry in page.Items)
        {
            var item = new ListViewItem(entry.Timestamp.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));
            item.SubItems.Add(entry.Source);
            item.SubItems.Add(entry.DeviceName ?? "—");
            item.SubItems.Add(entry.Action);
            item.SubItems.Add(entry.Result);
            item.SubItems.Add(entry.ErrorCode ?? "");
            _list.Items.Add(item);
        }

        _list.EndUpdate();
    }

    private async Task ExportAsync()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = string.Create(CultureInfo.InvariantCulture, $"auditoria-{_timeProvider.GetLocalNow():yyyyMMdd-HHmm}.csv"),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await using var file = File.Create(dialog.FileName);
        await AuditCsv.WriteAsync(file, _audit.StreamAsync(new AuditFilter(null, null, null, null, null), CancellationToken.None), CancellationToken.None);
    }
}
