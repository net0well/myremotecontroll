using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using MyRemoteControll.Agent.Infrastructure.Auth;

namespace MyRemoteControll.Agent.Infrastructure.Authorization;

/// <summary>Cada permissão vira a policy <c>perm:&lt;chave&gt;</c> (ADR-0009), criada sob demanda.</summary>
internal static class PermissionPolicies
{
    public const string Prefix = "perm:";

    public static string For(string permission) => Prefix + permission;

    /// <summary>Exige a permissão no endpoint REST.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(For(permission));
}

internal sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionPolicies.Prefix, StringComparison.Ordinal))
        {
            return await base.GetPolicyAsync(policyName).ConfigureAwait(false);
        }

        return new AuthorizationPolicyBuilder(DeviceTokenDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName[PermissionPolicies.Prefix.Length..]))
            .Build();
    }
}

internal sealed class PermissionAuthorizationHandler(DevicePermissionResolver resolver, IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.GetDeviceId() is not { } deviceId)
        {
            return;
        }

        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var access = await resolver.GetAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (access?.Has(requirement.Permission) == true)
        {
            context.Succeed(requirement);
        }
    }
}
