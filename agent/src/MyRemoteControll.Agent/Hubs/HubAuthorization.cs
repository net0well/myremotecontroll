using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using MyRemoteControll.Agent.Domain.Audit;
using MyRemoteControll.Agent.Infrastructure.Audit;
using MyRemoteControll.Agent.Infrastructure.Auth;
using MyRemoteControll.Agent.Infrastructure.Authorization;
using MyRemoteControll.Agent.Infrastructure.Logging;
using MyRemoteControll.Contracts.Errors;
using MyRemoteControll.Contracts.Hub;
using Serilog.Context;

namespace MyRemoteControll.Agent.Hubs;

/// <summary>Permissão exigida por um método do Hub (ADR-0009).</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class RequirePermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}

/// <summary>Método que qualquer aparelho autenticado pode chamar (ex.: assinar o painel, cujos cards já são filtrados).</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class AllowAnyDeviceAttribute : Attribute;

/// <summary>Grupos do SignalR por permissão, usados para enviar eventos só a quem pode recebê-los.</summary>
internal static class PermissionGroups
{
    public static string For(string permission) => $"perm:{permission}";
}

/// <summary>
/// Antes de cada método do Hub: correlation ID da invocação, checagem da permissão (sem atributo, nega) e auditoria
/// das negações. A negação volta como <see cref="HubResponse{T}"/> com <c>auth.forbidden</c>, como qualquer erro.
/// </summary>
internal sealed class PermissionHubFilter(DevicePermissionResolver resolver, IAuditLog auditLog) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var correlationId = CorrelationContext.NewId();
        CorrelationContext.Current = correlationId;
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            var method = invocationContext.HubMethod;
            var required = method.GetCustomAttribute<RequirePermissionAttribute>()?.Permission;
            var anyDevice = method.IsDefined(typeof(AllowAnyDeviceAttribute));
            var deviceId = invocationContext.Context.User.GetDeviceId();
            var access = deviceId is { } id
                ? await resolver.GetAsync(id, invocationContext.Context.ConnectionAborted).ConfigureAwait(false)
                : null;

            if (access is not null && (anyDevice || (required is not null && access.Has(required))))
            {
                return await next(invocationContext).ConfigureAwait(false);
            }

            return await HubDenial.DenyAsync<object>(auditLog, deviceId, method.Name, required, invocationContext.Context.ConnectionAborted)
                .ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Negação de um método do Hub: auditada como <c>auth.denied</c> (operação e permissão no alvo) e devolvida como
/// <c>auth.forbidden</c>. Usada pelo filtro e pelos métodos cuja permissão depende dos argumentos (ex.: energia).
/// </summary>
internal static class HubDenial
{
    public const string Message = "Este aparelho não tem permissão para esta ação.";

    public static async Task<HubResponse<T>> DenyAsync<T>(
        IAuditLog auditLog,
        Guid? deviceId,
        string operation,
        string? permission,
        CancellationToken cancellationToken)
    {
        await auditLog.WriteAsync(
            new AuditRecord(AuditActions.AccessDenied, AuditResult.Denied)
            {
                DeviceId = deviceId,
                ErrorCode = ErrorCodes.AuthForbidden,
                Target = new { operation, permission },
            },
            cancellationToken).ConfigureAwait(false);

        return HubResponse.Failure<T>(ErrorCodes.AuthForbidden, Message);
    }
}
