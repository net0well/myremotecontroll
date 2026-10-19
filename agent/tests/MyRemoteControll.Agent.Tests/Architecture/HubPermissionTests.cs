using System.Reflection;
using MyRemoteControll.Agent.Hubs;
using MyRemoteControll.Contracts.Security;

namespace MyRemoteControll.Agent.Tests.Architecture;

public sealed class HubPermissionTests
{
    private static IEnumerable<MethodInfo> HubMethods() =>
        typeof(RemoteHub)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetBaseDefinition().DeclaringType == typeof(RemoteHub));

    [Fact]
    public void AC5_EveryHubMethod_DeclaresItsPermission()
    {
        var undeclared = HubMethods()
            .Where(m => !m.IsDefined(typeof(RequirePermissionAttribute)) && !m.IsDefined(typeof(AllowAnyDeviceAttribute)))
            .Select(m => m.Name)
            .ToList();

        Assert.True(undeclared.Count == 0, $"Métodos do Hub sem permissão declarada: {string.Join(", ", undeclared)}");
    }

    [Fact]
    public void DeclaredPermissions_ExistInTheCatalog()
    {
        var unknown = HubMethods()
            .Select(m => m.GetCustomAttribute<RequirePermissionAttribute>()?.Permission)
            .OfType<string>()
            .Where(p => !PermissionKeys.All.Contains(p))
            .ToList();

        Assert.Empty(unknown);
    }
}
