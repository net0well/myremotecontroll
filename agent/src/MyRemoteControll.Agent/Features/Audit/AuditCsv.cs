using System.Globalization;
using System.Text;
using MyRemoteControll.Contracts.Audit;

namespace MyRemoteControll.Agent.Features.Audit;

/// <summary>
/// Exportação CSV (RFC 4180, separador vírgula, UTF-8 com BOM para o Excel reconhecer acentos). Datas em ISO 8601 UTC.
/// </summary>
internal static class AuditCsv
{
    private static readonly string[] Header =
    [
        "Id", "DataHoraUtc", "Origem", "AparelhoId", "Aparelho", "Acao", "Resultado", "CodigoErro",
        "ConfirmacaoLocal", "CorrelationId", "Alvo",
    ];

    public static async Task WriteAsync(Stream output, IAsyncEnumerable<AuditEntryDto> entries, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        await writer.WriteLineAsync(string.Join(',', Header)).ConfigureAwait(false);
        await foreach (var entry in entries.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            string?[] fields =
            [
                entry.Id.ToString(CultureInfo.InvariantCulture),
                entry.Timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                entry.Source,
                entry.DeviceId?.ToString(),
                entry.DeviceName,
                entry.Action,
                entry.Result,
                entry.ErrorCode,
                entry.LocalConfirmation ? "sim" : "não",
                entry.CorrelationId,
                entry.Target?.GetRawText(),
            ];
            await writer.WriteLineAsync(string.Join(',', fields.Select(Escape))).ConfigureAwait(false);
        }
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        // Prefixos de fórmula viram texto, para o CSV aberto no Excel não executar nada.
        if (value[0] is '=' or '+' or '-' or '@')
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 || value[0] == '\''
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }
}
