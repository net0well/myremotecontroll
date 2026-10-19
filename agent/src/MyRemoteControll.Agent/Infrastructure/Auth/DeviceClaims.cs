using System.Security.Claims;
using MyRemoteControll.Agent.Domain.Devices;
using MyRemoteControll.Contracts.Devices;

namespace MyRemoteControll.Agent.Infrastructure.Auth;

internal static class DeviceClaims
{
    public const string DeviceIdType = "mrc:device_id";

    public static ClaimsPrincipal CreatePrincipal(Device device, string authenticationScheme) =>
        new(new ClaimsIdentity(
            [
                new Claim(DeviceIdType, device.Id.ToString()),
                new Claim(ClaimTypes.Name, device.Name),
                new Claim(ClaimTypes.Role, device.Role.ToString()),
            ],
            authenticationScheme));

    public static Guid? GetDeviceId(this ClaimsPrincipal? user) =>
        Guid.TryParse(user?.FindFirstValue(DeviceIdType), out var id) ? id : null;

    public static DeviceRole? GetDeviceRole(this ClaimsPrincipal? user) =>
        Enum.TryParse<DeviceRole>(user?.FindFirstValue(ClaimTypes.Role), out var role) ? role : null;
}
