using Microsoft.EntityFrameworkCore.Design;

namespace MyRemoteControll.Agent.Infrastructure.Persistence;

/// <summary>
/// Usada só pelo <c>dotnet ef</c>: sem ela a ferramenta executaria o <c>Main</c>, que abre a bandeja.
/// </summary>
internal sealed class AgentDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AgentDbContext>
{
    public AgentDbContext CreateDbContext(string[] args) =>
        new(SqliteDatabase.ContextOptions(SqliteDatabase.ConnectionString("design-time.db")));
}
