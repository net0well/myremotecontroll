using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyRemoteControll.Agent.Infrastructure.Persistence;

namespace MyRemoteControll.Agent.Features.Health;

internal sealed class DatabaseHealthCheck(AgentDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Banco de dados inacessível.");
}
