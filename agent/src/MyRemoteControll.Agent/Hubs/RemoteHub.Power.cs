using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using MyRemoteControll.Agent.Features.Power;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Agent.Infrastructure.Auth;
using MyRemoteControll.Agent.Infrastructure.Authorization;
using MyRemoteControll.Contracts.Errors;
using MyRemoteControll.Contracts.Hub;
using MyRemoteControll.Contracts.Power;
using MyRemoteControll.Contracts.Security;

namespace MyRemoteControll.Agent.Hubs;

/// <summary>Ações de energia (spec 005).</summary>
internal sealed partial class RemoteHub
{
    /// <summary>
    /// O filtro exige <c>power.basic</c>; desligar, reiniciar e hibernar exigem também <c>power.shutdown</c>, conferido
    /// aqui porque depende da ação pedida.
    /// </summary>
    [RequirePermission(PermissionKeys.PowerBasic)]
    public async Task<HubResponse<PowerCountdownDto>> PowerAction(
        PowerActionRequest request,
        [FromServices] PowerService power,
        [FromServices] DevicePermissionResolver resolver,
        [FromServices] IValidator<PowerActionRequest> validator,
        [FromServices] IAuditLog auditLog)
    {
        var cancellationToken = Context.ConnectionAborted;
        var validation = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return HubResponse.Failure<PowerCountdownDto>(ErrorCodes.ValidationFailed, string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var deviceId = Context.User.GetDeviceId();
        var required = PowerService.RequiredPermission(request.Action);
        var access = deviceId is { } id ? await resolver.GetAsync(id, cancellationToken).ConfigureAwait(false) : null;
        if (access is null || !access.Has(required))
        {
            return await HubDenial.DenyAsync<PowerCountdownDto>(auditLog, deviceId, nameof(PowerAction), required, cancellationToken)
                .ConfigureAwait(false);
        }

        var requester = new PowerRequester(deviceId, Context.User?.Identity?.Name, request.LocalConfirmation);
        var result = await power.RequestAsync(request, requester, cancellationToken).ConfigureAwait(false);
        return result.ToHubResponse();
    }

    [RequirePermission(PermissionKeys.PowerBasic)]
    public async Task<HubResponse<PowerCancelledDto>> CancelPowerAction([FromServices] PowerService power)
    {
        var result = await power
            .CancelAsync(AuditActor.Device(Context.User.GetDeviceId()), PowerCancelSource.Device, Context.ConnectionAborted)
            .ConfigureAwait(false);
        return result.ToHubResponse();
    }
}
