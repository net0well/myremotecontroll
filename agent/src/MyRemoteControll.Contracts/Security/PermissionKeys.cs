using MyRemoteControll.Contracts.Devices;

namespace MyRemoteControll.Contracts.Security;

/// <summary>Permissões da seção 9 do documento base (ADR-0009).</summary>
public static class PermissionKeys
{
    public const string StatusView = "status.view";
    public const string MonitorView = "monitor.view";
    public const string ScreenView = "screen.view";
    public const string ScreenControl = "screen.control";
    public const string PowerBasic = "power.basic";
    public const string PowerShutdown = "power.shutdown";
    public const string ProcessesView = "processes.view";
    public const string ProcessesKill = "processes.kill";
    public const string FavoritesOpen = "favorites.open";
    public const string FavoritesManage = "favorites.manage";
    public const string FilesRead = "files.read";
    public const string FilesWrite = "files.write";
    public const string FilesDelete = "files.delete";
    public const string ClaudeView = "claude.view";
    public const string ClaudeChat = "claude.chat";
    public const string ClaudeApprove = "claude.approve";
    public const string GitRead = "dev.git.read";
    public const string GitWrite = "dev.git.write";
    public const string GitDiscard = "dev.git.discard";
    public const string Builds = "dev.builds";
    public const string Preview = "dev.preview";
    public const string SchedulesManage = "schedules.manage";
    public const string AlertsManage = "alerts.manage";
    public const string SecurityViewAudit = "security.viewAudit";
    public const string SecurityManageDevices = "security.manageDevices";
    public const string SecurityPanic = "security.panic";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        StatusView, MonitorView, ScreenView, ScreenControl, PowerBasic, PowerShutdown, ProcessesView, ProcessesKill,
        FavoritesOpen, FavoritesManage, FilesRead, FilesWrite, FilesDelete, ClaudeView, ClaudeChat, ClaudeApprove,
        GitRead, GitWrite, GitDiscard, Builds, Preview, SchedulesManage, AlertsManage, SecurityViewAudit,
        SecurityManageDevices, SecurityPanic,
    };
}

/// <summary>Permissões de cada papel (matriz da seção 9). Permissões extras por aparelho somam-se a estas.</summary>
public static class RolePermissions
{
    private static readonly HashSet<string> ReadOnly = new(StringComparer.Ordinal)
    {
        PermissionKeys.StatusView, PermissionKeys.MonitorView, PermissionKeys.ScreenView, PermissionKeys.ProcessesView,
        PermissionKeys.ClaudeView, PermissionKeys.GitRead, PermissionKeys.SecurityPanic,
    };

    private static readonly HashSet<string> Operator = new(ReadOnly, StringComparer.Ordinal)
    {
        PermissionKeys.PowerBasic, PermissionKeys.PowerShutdown, PermissionKeys.ProcessesKill, PermissionKeys.FavoritesOpen,
        PermissionKeys.FilesRead, PermissionKeys.FilesWrite, PermissionKeys.ClaudeChat, PermissionKeys.GitWrite,
        PermissionKeys.Builds, PermissionKeys.Preview, PermissionKeys.SchedulesManage,
    };

    public static IReadOnlySet<string> For(DeviceRole role) =>
        role switch
        {
            DeviceRole.Administrator => PermissionKeys.All,
            DeviceRole.Operator => Operator,
            _ => ReadOnly,
        };
}
