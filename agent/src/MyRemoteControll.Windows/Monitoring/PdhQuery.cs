using Windows.Win32;
using Windows.Win32.System.Performance;

namespace MyRemoteControll.Windows.Monitoring;

/// <summary>
/// Consulta PDH com contadores pelo nome em inglês (<c>PdhAddEnglishCounter</c>), que não muda com o idioma do Windows.
/// Contador que não existe neste PC fica de fora e sempre lê como ausente.
/// </summary>
internal sealed class PdhQuery : IDisposable
{
    private readonly PdhCloseQuerySafeHandle _query;

    public PdhQuery()
    {
        var status = PInvoke.PdhOpenQuery(null, 0, out _query);
        if (status != 0)
        {
            throw new InvalidOperationException($"Não foi possível abrir a consulta de desempenho (PDH 0x{status:X8}).");
        }
    }

    public PDH_HCOUNTER? TryAdd(string englishPath) =>
        PInvoke.PdhAddEnglishCounter(_query, englishPath, 0, out var counter) == 0 ? counter : null;

    /// <summary>Falso se a coleta falhou; os contadores então leem como ausentes.</summary>
    public bool Collect() => PInvoke.PdhCollectQueryData(new PDH_HQUERY(_query.DangerousGetHandle())) == 0;

    public static double? Value(PDH_HCOUNTER? counter)
    {
        if (counter is not { } handle
            || PInvoke.PdhGetFormattedCounterValue(handle, PDH_FMT.PDH_FMT_DOUBLE, out var value) != 0
            || !IsValid(value.CStatus))
        {
            return null;
        }

        return value.doubleValue;
    }

    /// <summary>Todas as instâncias de um contador com curinga (ex.: <c>Processor Information(*)</c>).</summary>
    public static unsafe IReadOnlyDictionary<string, double> Values(PDH_HCOUNTER? counter)
    {
        Dictionary<string, double> values = new(StringComparer.Ordinal);
        if (counter is not { } handle)
        {
            return values;
        }

        uint size = 0;
        uint count = 0;
        if (PInvoke.PdhGetFormattedCounterArray(handle, PDH_FMT.PDH_FMT_DOUBLE, &size, &count, null) != PInvoke.PDH_MORE_DATA || size == 0)
        {
            return values;
        }

        var buffer = new byte[size];
        fixed (byte* bytes = buffer)
        {
            var items = (PDH_FMT_COUNTERVALUE_ITEM_W*)bytes;
            if (PInvoke.PdhGetFormattedCounterArray(handle, PDH_FMT.PDH_FMT_DOUBLE, &size, &count, items) != 0)
            {
                return values;
            }

            // Os nomes apontam para dentro do mesmo buffer: lidos antes de sair do bloco fixed.
            for (var i = 0; i < count; i++)
            {
                if (IsValid(items[i].FmtValue.CStatus))
                {
                    values[items[i].szName.ToString()] = items[i].FmtValue.doubleValue;
                }
            }
        }

        return values;
    }

    public void Dispose() => _query.Dispose();

    private static bool IsValid(uint status) => status is PInvoke.PDH_CSTATUS_VALID_DATA or PInvoke.PDH_CSTATUS_NEW_DATA;
}
