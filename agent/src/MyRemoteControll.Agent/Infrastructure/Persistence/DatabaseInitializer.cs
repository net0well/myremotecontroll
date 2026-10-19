using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MyRemoteControll.Agent.Infrastructure.Persistence;

/// <summary>
/// Prepara o banco antes do host subir: preserva arquivo corrompido, faz backup antes de migrations pendentes,
/// aplica as migrations e restaura o backup se algo falhar (spec 001, AC-3 e AC-7).
/// </summary>
internal sealed partial class DatabaseInitializer(TimeProvider timeProvider, ILogger<DatabaseInitializer> logger)
{
    private const int BackupsToKeep = 3;

    public async Task<DatabaseInitializationResult> InitializeAsync(string databasePath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(databasePath)
            ?? throw new ArgumentException("O caminho do banco precisa incluir um diretório.", nameof(databasePath));
        Directory.CreateDirectory(directory);

        var existed = File.Exists(databasePath);
        string? preservedCorruptFile = null;
        if (existed && !await IsHealthyAsync(databasePath, cancellationToken).ConfigureAwait(false))
        {
            preservedCorruptFile = PreserveCorruptFile(databasePath);
            existed = false;
        }

        string? backupPath = null;
        var options = SqliteDatabase.ContextOptions(SqliteDatabase.ConnectionString(databasePath));
        await using (var db = new AgentDbContext(options))
        {
            var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
            if (existed && pending.Any())
            {
                backupPath = await BackupAsync(databasePath, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogMigrationFailed(ex);
                if (backupPath is not null)
                {
                    Restore(backupPath, databasePath);
                }

                throw new DatabaseInitializationException("Falha ao atualizar o banco de dados do agente.", ex);
            }
        }

        return new DatabaseInitializationResult(!existed, preservedCorruptFile, backupPath);
    }

    private async Task<bool> IsHealthyAsync(string databasePath, CancellationToken cancellationToken)
    {
        // Sem pool: o arquivo precisa estar livre para ser renomeado caso esteja corrompido.
        await using var connection = new SqliteConnection(SqliteDatabase.ConnectionString(databasePath, pooling: false));
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            return string.Equals(result, "ok", StringComparison.Ordinal);
        }
        catch (SqliteException ex)
        {
            LogIntegrityCheckFailed(ex, databasePath);
            return false;
        }
    }

    private string PreserveCorruptFile(string databasePath)
    {
        var target = UniqueSiblingPath(databasePath, "corrupt");
        File.Move(databasePath, target);
        foreach (var suffix in (string[])["-wal", "-shm"])
        {
            if (File.Exists(databasePath + suffix))
            {
                File.Move(databasePath + suffix, target + suffix);
            }
        }

        LogCorruptFilePreserved(target);
        return target;
    }

    private async Task<string> BackupAsync(string databasePath, CancellationToken cancellationToken)
    {
        var backupPath = UniqueSiblingPath(databasePath, "backup");
        await using (var source = new SqliteConnection(SqliteDatabase.ConnectionString(databasePath, pooling: false)))
        await using (var target = new SqliteConnection(SqliteDatabase.ConnectionString(backupPath, pooling: false)))
        {
            await source.OpenAsync(cancellationToken).ConfigureAwait(false);
            await target.OpenAsync(cancellationToken).ConfigureAwait(false);
            source.BackupDatabase(target);
        }

        PruneBackups(databasePath);
        LogBackupCreated(backupPath);
        return backupPath;
    }

    private void Restore(string backupPath, string databasePath)
    {
        SqliteConnection.ClearAllPools();
        File.Copy(backupPath, databasePath, overwrite: true);
        foreach (var suffix in (string[])["-wal", "-shm"])
        {
            File.Delete(databasePath + suffix);
        }

        LogBackupRestored(backupPath);
    }

    private static void PruneBackups(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath)!;
        var name = Path.GetFileNameWithoutExtension(databasePath);
        var oldBackups = new DirectoryInfo(directory)
            .GetFiles($"{name}.backup-*.db")
            .OrderByDescending(f => f.Name, StringComparer.Ordinal)
            .Skip(BackupsToKeep);
        foreach (var file in oldBackups)
        {
            file.Delete();
        }
    }

    private string UniqueSiblingPath(string databasePath, string kind)
    {
        var directory = Path.GetDirectoryName(databasePath)!;
        var name = Path.GetFileNameWithoutExtension(databasePath);
        var stamp = timeProvider.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var candidate = Path.Combine(directory, $"{name}.{kind}-{stamp}.db");
        for (var i = 1; File.Exists(candidate); i++)
        {
            candidate = Path.Combine(directory, $"{name}.{kind}-{stamp}-{i}.db");
        }

        return candidate;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Verificação de integridade falhou para {DatabasePath}")]
    private partial void LogIntegrityCheckFailed(Exception exception, string databasePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Banco corrompido preservado em {PreservedPath}; um banco novo será criado")]
    private partial void LogCorruptFilePreserved(string preservedPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backup do banco criado em {BackupPath} antes das migrations")]
    private partial void LogBackupCreated(string backupPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao aplicar as migrations do banco")]
    private partial void LogMigrationFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Backup {BackupPath} restaurado após falha nas migrations")]
    private partial void LogBackupRestored(string backupPath);
}
