using MyRemoteControll.Contracts.Devices;
using MyRemoteControll.Contracts.Security;

namespace MyRemoteControll.Agent.Tests.Security;

/// <summary>Matriz da seção 9 do documento base.</summary>
public sealed class RolePermissionsTests
{
    [Theory]
    [InlineData(PermissionKeys.StatusView, true, true, true)]
    [InlineData(PermissionKeys.ScreenView, true, true, true)]
    [InlineData(PermissionKeys.ScreenControl, false, false, true)]
    [InlineData(PermissionKeys.PowerBasic, false, true, true)]
    [InlineData(PermissionKeys.PowerShutdown, false, true, true)]
    [InlineData(PermissionKeys.ProcessesKill, false, true, true)]
    [InlineData(PermissionKeys.FavoritesManage, false, false, true)]
    [InlineData(PermissionKeys.FilesWrite, false, true, true)]
    [InlineData(PermissionKeys.FilesDelete, false, false, true)]
    [InlineData(PermissionKeys.ClaudeChat, false, true, true)]
    [InlineData(PermissionKeys.ClaudeApprove, false, false, true)]
    [InlineData(PermissionKeys.GitRead, true, true, true)]
    [InlineData(PermissionKeys.GitDiscard, false, false, true)]
    [InlineData(PermissionKeys.AlertsManage, false, false, true)]
    [InlineData(PermissionKeys.SecurityManageDevices, false, false, true)]
    [InlineData(PermissionKeys.SecurityPanic, true, true, true)]
    public void Matrix_FollowsSection9(string permission, bool readOnly, bool @operator, bool administrator)
    {
        Assert.Equal(readOnly, RolePermissions.For(DeviceRole.ReadOnly).Contains(permission));
        Assert.Equal(@operator, RolePermissions.For(DeviceRole.Operator).Contains(permission));
        Assert.Equal(administrator, RolePermissions.For(DeviceRole.Administrator).Contains(permission));
    }
}
