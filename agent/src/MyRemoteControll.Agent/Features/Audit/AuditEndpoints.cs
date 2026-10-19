using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using MyRemoteControll.Agent.Domain.Audit;
using MyRemoteControll.Agent.Infrastructure.Authorization;
using MyRemoteControll.Agent.Infrastructure.Web;
using MyRemoteControll.Contracts.Audit;
using MyRemoteControll.Contracts.Security;

namespace MyRemoteControll.Agent.Features.Audit;

internal sealed class AuditEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/audit").RequirePermission(PermissionKeys.SecurityViewAudit);

        group.MapGet("/", PageAsync).WithName("GetAudit");
        group.MapGet("/export.csv", Export).WithName("ExportAudit");
    }

    private static async Task<Ok<AuditPage>> PageAsync(
        AuditQuery audit,
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? deviceId,
        string? action,
        AuditResult? result,
        string? cursor,
        int? limit,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await audit
            .PageAsync(new AuditFilter(from, to, deviceId, action, result), cursor, limit, cancellationToken)
            .ConfigureAwait(false));

    private static PushStreamHttpResult Export(
        AuditQuery audit,
        TimeProvider timeProvider,
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? deviceId,
        string? action,
        AuditResult? result,
        CancellationToken cancellationToken)
    {
        var filter = new AuditFilter(from, to, deviceId, action, result);
        var fileName = string.Create(CultureInfo.InvariantCulture, $"auditoria-{timeProvider.GetLocalNow():yyyyMMdd-HHmm}.csv");
        return TypedResults.Stream(
            stream => AuditCsv.WriteAsync(stream, audit.StreamAsync(filter, cancellationToken), cancellationToken),
            "text/csv; charset=utf-8",
            fileName);
    }
}
