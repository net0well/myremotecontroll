using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using MyRemoteControll.Windows.Processes;

namespace MyRemoteControll.Agent.Features.Pairing;

internal interface IHostAddressProvider
{
    /// <summary>Endereços pelos quais o celular pode alcançar o PC, na ordem em que o app deve tentar.</summary>
    Task<IReadOnlyList<string>> GetHostsAsync(CancellationToken cancellationToken);
}

/// <summary>
/// IPv4 privados das interfaces ativas (LAN), IP do Tailscale (100.64.0.0/10) e o nome MagicDNS, quando o Tailscale
/// está instalado. Adaptadores virtuais (Hyper-V, WSL, VirtualBox, VMware) ficam de fora: o celular não os alcança.
/// </summary>
internal sealed partial class HostAddressProvider(IProcessRunner processRunner, ILogger<HostAddressProvider> logger)
    : IHostAddressProvider
{
    private static readonly TimeSpan TailscaleTimeout = TimeSpan.FromSeconds(3);
    private static readonly string[] VirtualAdapterMarkers = ["Hyper-V", "vEthernet", "VirtualBox", "VMware", "WSL"];

    public async Task<IReadOnlyList<string>> GetHostsAsync(CancellationToken cancellationToken)
    {
        List<string> lan = [];
        List<string> tailscale = [];
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up
                || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback
                || IsVirtual(networkInterface))
            {
                continue;
            }

            foreach (var address in networkInterface.GetIPProperties().UnicastAddresses
                         .Select(a => a.Address)
                         .Where(a => a.AddressFamily == AddressFamily.InterNetwork))
            {
                if (IsTailscale(address))
                {
                    tailscale.Add(address.ToString());
                }
                else if (IsPrivate(address))
                {
                    lan.Add(address.ToString());
                }
            }
        }

        var magicDns = await GetTailscaleDnsNameAsync(cancellationToken).ConfigureAwait(false);
        return [.. lan.Distinct(), .. tailscale.Distinct(), .. magicDns is null ? [] : new[] { magicDns }];
    }

    internal static bool IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }

    internal static bool IsTailscale(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 100 && bytes[1] is >= 64 and <= 127;
    }

    private static bool IsVirtual(NetworkInterface networkInterface) =>
        VirtualAdapterMarkers.Any(marker =>
            networkInterface.Name.Contains(marker, StringComparison.OrdinalIgnoreCase)
            || networkInterface.Description.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private async Task<string?> GetTailscaleDnsNameAsync(CancellationToken cancellationToken)
    {
        var executable = FindTailscale();
        if (executable is null)
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TailscaleTimeout);
        try
        {
            var result = await processRunner.RunAsync(executable, ["status", "--json"], timeout.Token).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                return null;
            }

            using var status = JsonDocument.Parse(result.StandardOutput);
            var dnsName = status.RootElement.TryGetProperty("Self", out var self) && self.TryGetProperty("DNSName", out var name)
                ? name.GetString()
                : null;
            return string.IsNullOrWhiteSpace(dnsName) ? null : dnsName.TrimEnd('.');
        }
        catch (Exception ex) when (ex is OperationCanceledException or JsonException or Win32Exception or InvalidOperationException)
        {
            LogTailscaleUnavailable(ex);
            return null;
        }
    }

    private static string? FindTailscale()
    {
        var installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale.exe");
        if (File.Exists(installed))
        {
            return installed;
        }

        return (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, "tailscale.exe"))
            .FirstOrDefault(File.Exists);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Não foi possível consultar o nome MagicDNS do Tailscale")]
    private partial void LogTailscaleUnavailable(Exception exception);
}
