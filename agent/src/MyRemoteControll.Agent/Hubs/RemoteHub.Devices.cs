using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using MyRemoteControll.Agent.Features.Devices;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Agent.Infrastructure.Auth;
using MyRemoteControll.Contracts.Devices;
using MyRemoteControll.Contracts.Errors;
using MyRemoteControll.Contracts.Hub;
using MyRemoteControll.Contracts.Security;

namespace MyRemoteControll.Agent.Hubs;

/// <summary>Segurança → aparelhos (spec 003).</summary>
internal sealed partial class RemoteHub
{
    [RequirePermission(PermissionKeys.SecurityManageDevices)]
    public async Task<HubResponse<IReadOnlyList<DeviceDto>>> ListDevices([FromServices] DeviceManagement devices) =>
        HubResponse.Success(await devices.ListAsync(Context.ConnectionAborted).ConfigureAwait(false));

    [RequirePermission(PermissionKeys.SecurityManageDevices)]
    public async Task<HubResponse<DeviceDto>> UpdateDevicePermissions(
        Guid id,
        UpdateDevicePermissionsRequest request,
        [FromServices] DeviceManagement devices,
        [FromServices] IValidator<UpdateDevicePermissionsRequest> validator)
    {
        var validation = await validator.ValidateAsync(request, Context.ConnectionAborted).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return HubResponse.Failure<DeviceDto>(ErrorCodes.ValidationFailed, string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var result = await devices
            .UpdatePermissionsAsync(id, request, AuditActor.Device(Context.User.GetDeviceId()), Context.ConnectionAborted)
            .ConfigureAwait(false);
        return result.ToHubResponse();
    }
}
