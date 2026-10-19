namespace MyRemoteControll.Windows.Processes;

/// <summary>
/// Executa programas externos sempre com lista de argumentos (nunca string de shell), para que nenhum valor
/// vindo de fora possa injetar comandos.
/// </summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
