using System.Globalization;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Registry;

namespace MyRemoteControll.Windows.Monitoring;

/// <summary>Nome, Windows, ligado desde, usuário, endereços, bateria e plano de energia do PC.</summary>
public sealed class WindowsSystemStatusSource(TimeProvider timeProvider) : ISystemStatusSource
{
    // A partir desta build o Windows é o 11, mas o registro ainda diz "Windows 10" no ProductName.
    private const int Windows11FirstBuild = 22000;
    private const byte NoSystemBattery = 128;
    private const byte UnknownBatteryStatus = 255;
    private const byte BatteryCharging = 8;
    private const uint UnknownLifeTime = uint.MaxValue;

    // Calculado uma vez: relido a cada chamada, variaria alguns milissegundos e pareceria uma mudança de status.
    private readonly DateTimeOffset _bootedAt = TruncateToSeconds(timeProvider.GetUtcNow() - TimeSpan.FromMilliseconds(Environment.TickCount64));

    public SystemStatusSample GetStatus()
    {
        var (osName, osVersion) = OperatingSystemName();
        var (battery, onAcPower) = PowerStatus();
        return new SystemStatusSample(
            Environment.MachineName,
            osName,
            osVersion,
            _bootedAt,
            $@"{Environment.UserDomainName}\{Environment.UserName}",
            Addresses(),
            battery,
            onAcPower,
            ActivePowerPlan());
    }

    private static DateTimeOffset TruncateToSeconds(DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), value.Offset);

    private static (string Name, string Version) OperatingSystemName()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var product = key?.GetValue("ProductName") as string ?? "Windows";
        var displayVersion = key?.GetValue("DisplayVersion") as string;
        var build = int.TryParse(key?.GetValue("CurrentBuildNumber") as string, CultureInfo.InvariantCulture, out var number) ? number : 0;
        var revision = key?.GetValue("UBR") is int ubr ? ubr : 0;

        if (build >= Windows11FirstBuild)
        {
            product = product.Replace("Windows 10", "Windows 11", StringComparison.Ordinal);
        }

        var version = displayVersion is null
            ? $"build {build}.{revision}"
            : $"{displayVersion} (build {build}.{revision})";
        return (product, version);
    }

    private static List<InterfaceAddress> Addresses() =>
    [
        .. NetworkInterfaces.Relevant()
            .SelectMany(pair => pair.Interface.GetIPProperties().UnicastAddresses
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => new InterfaceAddress(pair.Interface.Name, pair.Kind, address.Address.ToString()))),
    ];

    private static (BatterySample? Battery, bool OnAcPower) PowerStatus()
    {
        if (!PInvoke.GetSystemPowerStatus(out var status))
        {
            return (null, true);
        }

        var onAcPower = status.ACLineStatus != 0;
        if (status.BatteryFlag is NoSystemBattery or UnknownBatteryStatus || status.BatteryLifePercent > 100)
        {
            return (null, onAcPower);
        }

        var charging = (status.BatteryFlag & BatteryCharging) != 0;
        int? remaining = status.BatteryLifeTime == UnknownLifeTime ? null : (int)status.BatteryLifeTime;
        return (new BatterySample(status.BatteryLifePercent, charging, remaining), onAcPower);
    }

    private static unsafe string? ActivePowerPlan()
    {
        Guid* scheme = null;
        if (PInvoke.PowerGetActiveScheme(HKEY.Null, &scheme) != WIN32_ERROR.ERROR_SUCCESS || scheme is null)
        {
            return null;
        }

        try
        {
            uint size = 0;
            if (PInvoke.PowerReadFriendlyName(HKEY.Null, scheme, null, null, null, &size) != WIN32_ERROR.ERROR_SUCCESS || size == 0)
            {
                return null;
            }

            var buffer = new byte[size];
            fixed (byte* bytes = buffer)
            {
                if (PInvoke.PowerReadFriendlyName(HKEY.Null, scheme, null, null, bytes, &size) != WIN32_ERROR.ERROR_SUCCESS)
                {
                    return null;
                }
            }

            return MemoryMarshal.Cast<byte, char>(buffer.AsSpan(0, (int)size)).ToString().TrimEnd('\0');
        }
        finally
        {
            PInvoke.LocalFree(new HLOCAL(scheme));
        }
    }
}
