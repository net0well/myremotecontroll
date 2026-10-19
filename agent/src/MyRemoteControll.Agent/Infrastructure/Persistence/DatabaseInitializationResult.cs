namespace MyRemoteControll.Agent.Infrastructure.Persistence;

/// <param name="Created">O banco não existia (ou estava corrompido) e foi criado agora.</param>
/// <param name="PreservedCorruptFile">Caminho para onde o arquivo corrompido foi movido, se houve.</param>
/// <param name="BackupPath">Backup feito antes de aplicar migrations pendentes, se houve.</param>
internal sealed record DatabaseInitializationResult(bool Created, string? PreservedCorruptFile, string? BackupPath);
