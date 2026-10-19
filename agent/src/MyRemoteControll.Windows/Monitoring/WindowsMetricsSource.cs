using System.Globalization;
using System.Text.RegularExpressions;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Performance;
using Windows.Win32.System.SystemInformation;

namespace MyRemoteControll.Windows.Monitoring;

/// <summary>
/// Métricas pelos contadores de desempenho do Windows (PDH), <c>GlobalMemoryStatusEx</c>, <c>DriveInfo</c> e as
/// estatísticas das interfaces de rede. Contadores de taxa só têm valor a partir da segunda coleta.
/// </summary>
public sealed partial class WindowsMetricsSource : IMetricsSource, IDisposable
{
    private const string TotalInstance = "_Total";

    private readonly TimeProvider _timeProvider;
    private readonly PdhQuery _query = new();
    private readonly PDH_HCOUNTER? _cpuUtility;
    private readonly PDH_HCOUNTER? _cpuFrequency;
    private readonly PDH_HCOUNTER? _cpuPerformance;
    private readonly PDH_HCOUNTER? _diskRead;
    private readonly PDH_HCOUNTER? _diskWrite;
    private readonly PDH_HCOUNTER? _gpuEngine;
    private readonly PDH_HCOUNTER? _gpuMemory;
    private readonly string? _gpuName = PrimaryDisplayAdapterName();
    private readonly Lock _gate = new();
    private Dictionary<string, (long Received, long Sent)> _previousNetwork = [];
    private long _previousNetworkTimestamp;

    public WindowsMetricsSource(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _cpuUtility = _query.TryAdd(@"\Processor Information(*)\% Processor Utility");
        _cpuFrequency = _query.TryAdd(@"\Processor Information(_Total)\Processor Frequency");
        _cpuPerformance = _query.TryAdd(@"\Processor Information(_Total)\% Processor Performance");
        _diskRead = _query.TryAdd(@"\LogicalDisk(*)\Disk Read Bytes/sec");
        _diskWrite = _query.TryAdd(@"\LogicalDisk(*)\Disk Write Bytes/sec");
        _gpuEngine = _query.TryAdd(@"\GPU Engine(*)\Utilization Percentage");
        _gpuMemory = _query.TryAdd(@"\GPU Adapter Memory(*)\Dedicated Usage");
        _query.Collect();
    }

    public MetricsSample Sample()
    {
        lock (_gate)
        {
            _query.Collect();
            return new MetricsSample(Cpu(), Memory(), Disks(), Network(), Gpu());
        }
    }

    public void Dispose() => _query.Dispose();

    // Instâncias "grupo,núcleo" (ex.: "0,11"); "0,_Total" e "_Total" ficam de fora.
    [GeneratedRegex(@"^(\d+),(\d+)$")]
    private static partial Regex CoreInstance();

    // "pid_1234_luid_0x00000000_0x0000D1A3_phys_0_eng_3_engtype_3D": placa (luid) e motor.
    [GeneratedRegex(@"luid_(0x[0-9A-Fa-f]+_0x[0-9A-Fa-f]+)_phys_(\d+)_eng_(\d+)")]
    private static partial Regex GpuEngineInstance();

    private static double Clamp(double percent) => Math.Clamp(percent, 0, 100);

    private static MemorySample? Memory()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!PInvoke.GlobalMemoryStatusEx(ref status))
        {
            return null;
        }

        return new MemorySample((long)(status.ullTotalPhys - status.ullAvailPhys), (long)status.ullTotalPhys);
    }

    private static string? PrimaryDisplayAdapterName()
    {
        var device = new DISPLAY_DEVICEW { cb = (uint)System.Runtime.InteropServices.Marshal.SizeOf<DISPLAY_DEVICEW>() };
        for (uint i = 0; PInvoke.EnumDisplayDevices(null, i, ref device, 0); i++)
        {
            if (device.StateFlags.HasFlag(DISPLAY_DEVICE_STATE_FLAGS.DISPLAY_DEVICE_PRIMARY_DEVICE))
            {
                return device.DeviceString.ToString();
            }
        }

        return null;
    }

    private CpuSample? Cpu()
    {
        var utility = PdhQuery.Values(_cpuUtility);
        var cores = utility
            .Select(pair => (Match: CoreInstance().Match(pair.Key), pair.Value))
            .Where(core => core.Match.Success)
            .OrderBy(core => int.Parse(core.Match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ThenBy(core => int.Parse(core.Match.Groups[2].Value, CultureInfo.InvariantCulture))
            .Select(core => Clamp(core.Value))
            .ToList();
        double? total = utility.TryGetValue(TotalInstance, out var value) ? Clamp(value) : null;

        var frequency = PdhQuery.Value(_cpuFrequency);
        var performance = PdhQuery.Value(_cpuPerformance);
        double? clock = frequency is { } mhz && performance is { } percent ? mhz * percent / 100 : null;

        return total is null && cores.Count == 0 && clock is null ? null : new CpuSample(total, cores, clock);
    }

    private List<DiskSample> Disks()
    {
        var reads = PdhQuery.Values(_diskRead);
        var writes = PdhQuery.Values(_diskWrite);
        List<DiskSample> disks = [];
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
            {
                continue;
            }

            // A instância do LogicalDisk é "C:", sem a barra.
            var instance = drive.Name.TrimEnd('\\');
            disks.Add(new DiskSample(
                instance,
                string.IsNullOrWhiteSpace(drive.VolumeLabel) ? null : drive.VolumeLabel,
                drive.TotalSize - drive.TotalFreeSpace,
                drive.TotalSize,
                reads.TryGetValue(instance, out var read) ? read : null,
                writes.TryGetValue(instance, out var write) ? write : null));
        }

        return disks;
    }

    private List<NetworkInterfaceSample> Network()
    {
        var now = _timeProvider.GetTimestamp();
        var elapsed = _previousNetworkTimestamp == 0 ? TimeSpan.Zero : _timeProvider.GetElapsedTime(_previousNetworkTimestamp, now);
        Dictionary<string, (long Received, long Sent)> current = [];
        List<NetworkInterfaceSample> samples = [];

        foreach (var (networkInterface, kind) in NetworkInterfaces.Relevant())
        {
            var statistics = networkInterface.GetIPStatistics();
            current[networkInterface.Id] = (statistics.BytesReceived, statistics.BytesSent);
            if (elapsed > TimeSpan.Zero && _previousNetwork.TryGetValue(networkInterface.Id, out var previous))
            {
                samples.Add(new NetworkInterfaceSample(
                    networkInterface.Name,
                    kind,
                    Math.Max(0, statistics.BytesReceived - previous.Received) / elapsed.TotalSeconds,
                    Math.Max(0, statistics.BytesSent - previous.Sent) / elapsed.TotalSeconds));
            }
        }

        _previousNetwork = current;
        _previousNetworkTimestamp = now;
        return samples;
    }

    /// <summary>Uso da placa = motor mais ocupado (somando os processos), como o Gerenciador de Tarefas.</summary>
    private GpuSample? Gpu()
    {
        var engines = PdhQuery.Values(_gpuEngine);
        var memory = PdhQuery.Values(_gpuMemory);
        if (engines.Count == 0 && memory.Count == 0)
        {
            return null;
        }

        double? usage = engines
            .Select(pair => (Match: GpuEngineInstance().Match(pair.Key), pair.Value))
            .Where(engine => engine.Match.Success)
            .GroupBy(engine => engine.Match.Value)
            .Select(engine => engine.Sum(e => e.Value))
            .DefaultIfEmpty(0)
            .Max();
        long? dedicated = memory.Count == 0 ? null : (long)memory.Values.Sum();

        return new GpuSample(_gpuName, Clamp(usage.Value), dedicated);
    }
}
