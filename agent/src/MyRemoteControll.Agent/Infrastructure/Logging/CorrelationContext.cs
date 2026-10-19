namespace MyRemoteControll.Agent.Infrastructure.Logging;

/// <summary>
/// Correlation ID da operação em andamento (requisição REST ou invocação do Hub), visível para logs e auditoria sem
/// precisar ser passado de mão em mão (ADR-0020).
/// </summary>
internal static class CorrelationContext
{
    private static readonly AsyncLocal<string?> Value = new();

    public static string? Current
    {
        get => Value.Value;
        set => Value.Value = value;
    }

    public static string NewId() => Guid.NewGuid().ToString("N");
}
