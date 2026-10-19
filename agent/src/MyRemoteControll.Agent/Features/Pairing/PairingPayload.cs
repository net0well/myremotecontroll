using System.Globalization;

namespace MyRemoteControll.Agent.Features.Pairing;

/// <summary>
/// Conteúdo do QR de pareamento, versão 1 (spec 002):
/// <c>mrc://pair?v=1&amp;h=&lt;hosts separados por vírgula&gt;&amp;p=&lt;porta&gt;&amp;c=&lt;código&gt;&amp;f=&lt;pin SPKI&gt;&amp;n=&lt;nome do PC&gt;</c>.
/// Código e pin já são base64url; hosts e nome são codificados para URL.
/// </summary>
internal static class PairingPayload
{
    public const int Version = 1;

    public static string Build(IReadOnlyList<string> hosts, int port, string code, string pin, string pcName) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"mrc://pair?v={Version}&h={string.Join(',', hosts.Select(Uri.EscapeDataString))}&p={port}&c={code}&f={pin}&n={Uri.EscapeDataString(pcName)}");
}
