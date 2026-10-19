using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using MyRemoteControll.Agent.Domain.Audit;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Agent.Infrastructure.Auth;
using MyRemoteControll.Agent.Infrastructure.Web;
using MyRemoteControll.Contracts.Errors;

namespace MyRemoteControll.Agent.Infrastructure.Authorization;

/// <summary>Toda negação de acesso no REST é auditada e responde ProblemDetails com <c>auth.forbidden</c> (spec 003).</summary>
internal sealed class AuditingAuthorizationResultHandler(IAuditLog auditLog) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Forbidden)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult).ConfigureAwait(false);
            return;
        }

        var endpoint = context.GetEndpoint();
        var permission = policy.Requirements.OfType<PermissionRequirement>().FirstOrDefault()?.Permission;
        await auditLog.WriteAsync(
            new AuditRecord(AuditActions.AccessDenied, AuditResult.Denied)
            {
                DeviceId = context.User.GetDeviceId(),
                ErrorCode = ErrorCodes.AuthForbidden,
                Target = new
                {
                    operation = endpoint?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? context.Request.Path.Value,
                    permission,
                },
            },
            context.RequestAborted).ConfigureAwait(false);

        await ApiProblems
            .Create(StatusCodes.Status403Forbidden, ErrorCodes.AuthForbidden, "Este aparelho não tem permissão para esta ação.")
            .ExecuteAsync(context)
            .ConfigureAwait(false);
    }
}
