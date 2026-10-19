using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Agent.Infrastructure.Auth;
using MyRemoteControll.Agent.Infrastructure.Authorization;
using MyRemoteControll.Agent.Infrastructure.Results;
using MyRemoteControll.Agent.Infrastructure.Web;
using MyRemoteControll.Contracts.Devices;
using MyRemoteControll.Contracts.Security;

namespace MyRemoteControll.Agent.Features.Devices;

internal sealed class DevicesEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/devices").RequirePermission(PermissionKeys.SecurityManageDevices);

        group.MapGet("/", ListAsync).WithName("ListDevices");
        group.MapDelete("/{id:guid}", RevokeAsync).WithName("RevokeDevice");
    }

    private static async Task<Ok<IReadOnlyList<DeviceDto>>> ListAsync(DeviceManagement devices, CancellationToken cancellationToken) =>
        TypedResults.Ok(await devices.ListAsync(cancellationToken).ConfigureAwait(false));

    private static async Task<Results<NoContent, ProblemHttpResult>> RevokeAsync(
        Guid id,
        DeviceManagement devices,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await devices.RevokeAsync(id, AuditActor.Device(http.User.GetDeviceId()), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }
}

internal sealed class UpdateDevicePermissionsRequestValidator : AbstractValidator<UpdateDevicePermissionsRequest>
{
    public UpdateDevicePermissionsRequestValidator()
    {
        RuleFor(r => r.Role).IsInEnum();
        RuleFor(r => r.ExtraPermissions).NotNull();
        RuleForEach(r => r.ExtraPermissions)
            .Must(PermissionKeys.All.Contains)
            .WithMessage("Permissão desconhecida: {PropertyValue}.");
        RuleFor(r => r.Name).MaximumLength(100);
    }
}
