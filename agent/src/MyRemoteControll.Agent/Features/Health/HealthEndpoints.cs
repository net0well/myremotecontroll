using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyRemoteControll.Agent.Infrastructure.Web;

namespace MyRemoteControll.Agent.Features.Health;

internal sealed class HealthEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteAsync })
            .AllowAnonymous()
            .LoopbackOnly();

    private static Task WriteAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new HealthResponse(report.Status.ToString()));

    private sealed record HealthResponse(string Status);
}
