using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MyRemoteControll.Agent.Infrastructure.Persistence;

internal static class SqliteDatabase
{
    public static string ConnectionString(string databasePath, bool pooling = true) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = pooling,
        }.ToString();

    public static DbContextOptions<AgentDbContext> ContextOptions(string connectionString) =>
        new DbContextOptionsBuilder<AgentDbContext>().UseSqlite(connectionString).Options;
}
