using System.Net.NetworkInformation;
using MyRemoteControll.Agent.Features.Status;
using MyRemoteControll.Contracts.Status;
using MyRemoteControll.Windows.Monitoring;

namespace MyRemoteControll.Agent.Tests.Status;

public sealed class MonitoringTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NetworkTotals_CountOnlyPhysicalInterfaces()
    {
        // O tráfego do Tailscale e da VPN já passa pela placa física: somar contaria duas vezes.
        var sample = Sample(interfaces:
        [
            new NetworkInterfaceSample("Ethernet", InterfaceKind.Ethernet, 1000.4, 200),
            new NetworkInterfaceSample("Wi-Fi", InterfaceKind.Wifi, 500, 100.6),
            new NetworkInterfaceSample("Tailscale", InterfaceKind.Tailscale, 900, 900),
            new NetworkInterfaceSample("VPN da empresa", InterfaceKind.Vpn, 700, 700),
        ]);

        var network = MonitoringMapper.ToMetrics(sample, At).Network!;

        Assert.Equal(1500, network.DownBytesPerSecond);
        Assert.Equal(301, network.UpBytesPerSecond);
        Assert.Equal(4, network.Interfaces!.Count);
        Assert.Equal(NetworkKind.Tailscale, network.Interfaces[2].Kind);
    }

    [Fact]
    public void Values_AreRoundedAtTheSourceToKeepTheJsonShort()
    {
        var sample = Sample(cpu: new CpuSample(12.345, [99.95, 0.04], 3599.6), disks: [new DiskSample("C:", null, 10, 20, 1234.5, null)]);

        var metrics = MonitoringMapper.ToMetrics(sample, At);

        Assert.Equal(12.3, metrics.Cpu!.UsagePercent);
        Assert.Equal([100.0, 0.0], metrics.Cpu.CoreUsagePercent);
        Assert.Equal(3600, metrics.Cpu.ClockMhz);
        Assert.Equal(1235, metrics.Disks[0].ReadBytesPerSecond);
        Assert.Null(metrics.Disks[0].WriteBytesPerSecond);
    }

    [Fact]
    public void LiteVersions_DropCoresAndInterfaces()
    {
        var metrics = MonitoringMapper.ToMetrics(
            Sample(cpu: new CpuSample(10, [10, 20], null), interfaces: [new NetworkInterfaceSample("Ethernet", InterfaceKind.Ethernet, 1, 1)]),
            At);

        Assert.Null(MonitoringMapper.Lite(metrics.Cpu)!.CoreUsagePercent);
        Assert.Equal(10, MonitoringMapper.Lite(metrics.Cpu)!.UsagePercent);
        Assert.Null(MonitoringMapper.Lite(metrics.Network)!.Interfaces);
    }

    [Fact]
    public void FirstSampleWithoutInterfaces_HasNoNetwork()
    {
        Assert.Null(MonitoringMapper.ToMetrics(Sample(), At).Network);
    }

    [Theory]
    [InlineData("Tailscale", "Tailscale Tunnel", NetworkInterfaceType.Unknown, InterfaceKind.Tailscale)]
    [InlineData("vEthernet (WSL)", "Hyper-V Virtual Ethernet Adapter", NetworkInterfaceType.Ethernet, InterfaceKind.Virtual)]
    [InlineData("VMware Network Adapter VMnet8", "VMware Virtual Ethernet Adapter", NetworkInterfaceType.Ethernet, InterfaceKind.Virtual)]
    [InlineData("Wi-Fi", "Intel(R) Wi-Fi 6E AX211", NetworkInterfaceType.Wireless80211, InterfaceKind.Wifi)]
    [InlineData("Ethernet", "Realtek PCIe GbE Family Controller", NetworkInterfaceType.Ethernet, InterfaceKind.Ethernet)]
    [InlineData("wg0", "WireGuard Tunnel", NetworkInterfaceType.Unknown, InterfaceKind.Vpn)]
    [InlineData("Loopback Pseudo-Interface 1", "Software Loopback Interface 1", NetworkInterfaceType.Loopback, InterfaceKind.Loopback)]
    public void NetworkInterfaces_AreClassifiedForThePanel(string name, string description, NetworkInterfaceType type, InterfaceKind expected) =>
        Assert.Equal(expected, NetworkInterfaces.Classify(name, description, type));

    [Fact]
    public void RealSourcesOnThisPc_ReturnPlausibleValues()
    {
        using var source = new WindowsMetricsSource(TimeProvider.System);
        source.Sample();
        Thread.Sleep(500);

        var sample = source.Sample();
        var status = new WindowsSystemStatusSource(TimeProvider.System).GetStatus();

        Assert.InRange(sample.Cpu!.TotalPercent!.Value, 0, 100);
        Assert.Equal(Environment.ProcessorCount, sample.Cpu.CorePercent.Count);
        Assert.True(sample.Memory!.TotalBytes > sample.Memory.UsedBytes);
        Assert.Contains(sample.Disks, d => d.Drive == Path.GetPathRoot(Environment.SystemDirectory)!.TrimEnd('\\'));
        Assert.Equal(Environment.MachineName, status.Hostname);
        Assert.StartsWith("Windows", status.OsName, StringComparison.Ordinal);
        Assert.True(status.BootedAt < DateTimeOffset.UtcNow);
    }

    private static MetricsSample Sample(
        CpuSample? cpu = null,
        IReadOnlyList<DiskSample>? disks = null,
        IReadOnlyList<NetworkInterfaceSample>? interfaces = null) =>
        new(cpu, null, disks ?? [], interfaces ?? [], null);
}
